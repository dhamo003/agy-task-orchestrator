using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FluentAssertions;
using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Core.Checkpointing;
using AntigravityTaskRunner.Core.Models;
using AntigravityTaskRunner.Core.Orchestration;
using AntigravityTaskRunner.Core.Pipeline;
using AntigravityTaskRunner.Core.Progress;
using AntigravityTaskRunner.Core.Prompts;
using AntigravityTaskRunner.Core.Retry;
using AntigravityTaskRunner.Core.Workflow;
using AntigravityTaskRunner.Terminal.Workspace;
using Runner.Markdown.Parser;
using Runner.Markdown.Writer;
using Runner.Markdown.Models;
using Runner.Logging;
using TaskStatus = Runner.Markdown.Models.TaskStatus;

namespace AntigravityTaskRunner.Core.Tests;

/// <summary>
/// Covers the production guarantees of the sequential orchestrator: strict one-at-a-
/// time execution, fail-stop (no skipping), capacity-limit pause/resume on the same
/// task, checkpointing, and crash recovery. Uses the REAL parser, writer, retry
/// policy, and checkpoint store against a temp workspace; only the pipeline is faked.
/// </summary>
public sealed class SequentialOrchestratorTests : IDisposable
{
    private static readonly int[] LinesTwoThree = [2, 3];
    private static readonly int[] SameAttemptTwice = [1, 1];

    private readonly string _dir;
    private readonly string _tasksFile;

    public SequentialOrchestratorTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "orch-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _tasksFile = Path.Combine(_dir, "tasks.md");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    // ---------------------------------------------------------------- helpers

    private sealed class StubBuildValidator : AntigravityTaskRunner.Terminal.Build.IBuildValidator
    {
        public IReadOnlySet<string>? TestFailureBaseline { get; set; }
        public int BaselineCaptures { get; private set; }

        public Task<AntigravityTaskRunner.Terminal.Build.TestBaseline> CaptureTestBaselineAsync(CancellationToken token = default)
        {
            BaselineCaptures++;
            return Task.FromResult(AntigravityTaskRunner.Terminal.Build.TestBaseline.Empty);
        }

        public Task<AntigravityTaskRunner.Terminal.Build.BuildValidationResult> ValidateAsync(CancellationToken token = default) =>
            Task.FromResult(AntigravityTaskRunner.Terminal.Build.BuildValidationResult.SkippedResult("stub"));
    }

    private sealed class FakePipeline : ITaskPipeline
    {
        private readonly Func<TaskItem, int, TaskExecutionResult> _behavior;
        public List<(int Line, int Attempt)> Calls { get; } = [];
        public int ConcurrentExecutions;
        public int MaxObservedConcurrency;

        public FakePipeline(Func<TaskItem, int, TaskExecutionResult> behavior) => _behavior = behavior;

        public async Task<TaskExecutionResult> ExecuteAsync(
            TaskItem task, WorkspaceSnapshot initialSnapshot, string prompt, int attempt,
            CancellationToken token = default)
        {
            var concurrent = Interlocked.Increment(ref ConcurrentExecutions);
            MaxObservedConcurrency = Math.Max(MaxObservedConcurrency, concurrent);
            try
            {
                Calls.Add((task.LineNumber, attempt));
                await Task.Delay(10, token); // give overlap a chance to be observed
                return _behavior(task, attempt);
            }
            finally
            {
                Interlocked.Decrement(ref ConcurrentExecutions);
            }
        }
    }

    private static TaskExecutionResult Success(TaskItem t, int attempt) =>
        new(t, true, TimeSpan.FromMilliseconds(5), null, attempt);

    private static TaskExecutionResult Failure(TaskItem t, int attempt, FailureKind kind = FailureKind.NoChanges) =>
        new(t, false, TimeSpan.FromMilliseconds(5), $"failed: {kind}", attempt, kind);

    private (SequentialOrchestrator Orchestrator, FakePipeline Pipeline, ProgressTracker Progress, JsonCheckpointStore Store, RunnerOptions Options)
        Build(Func<TaskItem, int, TaskExecutionResult> behavior, Action<RunnerOptions>? configure = null)
    {
        var options = new RunnerOptions
        {
            TasksFile = _tasksFile,
            WorkspacePath = _dir,
            Model = "test-model",
        };
        options.Retry.MaxRetries = 0;
        options.Retry.BackoffBaseSeconds = 0.001;
        options.Limits.PauseSeconds = 1; // pauses use ms-scale via small value in tests below
        configure?.Invoke(options);

        var ioptions = Microsoft.Extensions.Options.Options.Create(options);
        var pipeline = new FakePipeline(behavior);
        var progress = new ProgressTracker();
        var store = new JsonCheckpointStore(ioptions);
        var loggerMock = new Mock<ITaskLogger>();

        var analyzerMock = new Mock<IWorkspaceAnalyzer>();
        analyzerMock.Setup(a => a.TakeSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceSnapshot(new Dictionary<string, FileSnapshot>(), DateTime.UtcNow));

        var promptMock = new Mock<IPromptTemplateEngine>();
        promptMock.Setup(p => p.BuildPromptAsync(It.IsAny<TaskItem>(), It.IsAny<WorkspaceSnapshot>(),
                It.IsAny<RetryContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem t, WorkspaceSnapshot _, RetryContext c, CancellationToken _) =>
                $"prompt for {t.DisplayText} attempt {c?.Attempt ?? 1}");

        var orchestrator = new SequentialOrchestrator(
            new MarkdownTaskParser(),
            new MarkdownTaskWriter(),
            pipeline,
            new RetryPolicy(ioptions, loggerMock.Object),
            progress,
            analyzerMock.Object,
            promptMock.Object,
            store,
            new StubBuildValidator(),
            loggerMock.Object,
            ioptions);

        return (orchestrator, pipeline, progress, store, options);
    }

    private void WriteTasks(params string[] lines) =>
        File.WriteAllLines(_tasksFile, lines);

    private string[] ReadTasks() => File.ReadAllLines(_tasksFile);

    // ---------------------------------------------------------------- tests

    [Fact]
    public async Task RunAll_CompletesAllTasks_InOrder_AndMarksThem()
    {
        WriteTasks("- [ ] Task one", "- [ ] Task two", "- [ ] Task three");
        var (orchestrator, pipeline, _, store, _) = Build(Success);

        await orchestrator.RunAllAsync(CancellationToken.None);

        pipeline.Calls.Select(c => c.Line).Should().Equal(1, 2, 3);
        ReadTasks().Should().OnlyContain(l => l.StartsWith("- [x]"));
        (await store.LoadAsync()).Should().BeNull("checkpoint must be cleared after completion");
    }

    [Fact]
    public async Task RunAll_IsStrictlySequential_NeverOverlaps()
    {
        WriteTasks("- [ ] A", "- [ ] B", "- [ ] C", "- [ ] D");
        var (orchestrator, pipeline, _, _, _) = Build(Success);

        await orchestrator.RunAllAsync(CancellationToken.None);

        pipeline.MaxObservedConcurrency.Should().Be(1, "at most one task may ever be active");
    }

    [Fact]
    public async Task RunAll_HaltsPipelineOnFailure_AndNeverStartsNextTask()
    {
        WriteTasks("- [ ] Doomed task", "- [ ] Never reached");
        var (orchestrator, pipeline, progress, _, _) = Build(
            (t, a) => t.LineNumber == 1 ? Failure(t, a) : Success(t, a));

        PipelineHaltReport? halt = null;
        progress.PipelineHalted += (_, r) => halt = r;

        await orchestrator.RunAllAsync(CancellationToken.None);

        pipeline.Calls.Should().OnlyContain(c => c.Line == 1, "the pipeline must stop, not skip to the next task");
        var lines = ReadTasks();
        lines[0].Should().StartWith("- [!]");
        lines[1].Should().StartWith("- [ ]", "the following task must remain untouched");
        halt.Should().NotBeNull();
        halt!.Result.Failure.Should().Be(FailureKind.NoChanges);
        halt.SuggestedNextAction.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RunAll_RetriesWithContext_BeforeFailing()
    {
        WriteTasks("- [ ] Flaky task");
        int attempts = 0;
        var (orchestrator, pipeline, _, _, _) = Build(
            (t, a) => ++attempts < 3 ? Failure(t, a, FailureKind.MarkerMissing) : Success(t, a),
            o => o.Retry.MaxRetries = 3);

        await orchestrator.RunAllAsync(CancellationToken.None);

        pipeline.Calls.Select(c => c.Attempt).Should().Equal(1, 2, 3);
        ReadTasks()[0].Should().StartWith("- [x]");
    }

    [Fact]
    public async Task RunAll_HaltsOnPreexistingFailedTask_WithoutExecutingAnything()
    {
        WriteTasks("- [!] Previously failed", "- [ ] Next task");
        var (orchestrator, pipeline, progress, _, _) = Build(Success);

        PipelineHaltReport? halt = null;
        progress.PipelineHalted += (_, r) => halt = r;

        await orchestrator.RunAllAsync(CancellationToken.None);

        pipeline.Calls.Should().BeEmpty("a failed task blocks the pipeline; it is never skipped");
        halt.Should().NotBeNull();
        ReadTasks()[1].Should().StartWith("- [ ]");
    }

    [Fact]
    public async Task RunAll_ReattemptsFailedTask_WhenRetryFailedTasksEnabled()
    {
        WriteTasks("- [!] Previously failed", "- [ ] Next task");
        var (orchestrator, pipeline, _, _, _) = Build(Success, o => o.RetryFailedTasks = true);

        await orchestrator.RunAllAsync(CancellationToken.None);

        pipeline.Calls.Select(c => c.Line).Should().Equal(1, 2);
        ReadTasks().Should().OnlyContain(l => l.StartsWith("- [x]"));
    }

    [Fact]
    public async Task RunAll_ResumesInProgressTask_AfterInterruption()
    {
        WriteTasks("- [x] Done earlier", "- [/] Was interrupted", "- [ ] Later");
        var (orchestrator, pipeline, _, _, _) = Build(Success);

        await orchestrator.RunAllAsync(CancellationToken.None);

        pipeline.Calls.Select(c => c.Line).Should().Equal(LinesTwoThree,
            "the in-progress task resumes first — never skipped");
        ReadTasks().Should().OnlyContain(l => l.StartsWith("- [x]"));
    }

    [Fact]
    public async Task CapacityLimit_PausesAndResumesSameTask_WithoutConsumingRetries()
    {
        WriteTasks("- [ ] Rate limited task");
        int calls = 0;
        var (orchestrator, pipeline, _, _, _) = Build(
            (t, a) => ++calls == 1
                ? new TaskExecutionResult(t, false, TimeSpan.Zero, "rate limit exceeded", a,
                    FailureKind.CapacityLimit,
                    Limit: new AntigravityTaskRunner.Terminal.Detection.LimitDetection("rate limit exceeded", "429: rate limit exceeded"))
                : Success(t, a),
            o =>
            {
                o.Retry.MaxRetries = 0;       // no retries at all…
                o.Limits.PauseSeconds = 1;    // …but pauses are still allowed
            });

        await orchestrator.RunAllAsync(CancellationToken.None);

        calls.Should().Be(2, "the task must resume after the pause");
        pipeline.Calls.Select(c => c.Attempt).Should().Equal(SameAttemptTwice,
            "a capacity pause must not consume a retry attempt");
        ReadTasks()[0].Should().StartWith("- [x]", "the task must complete after resuming — never skipped");
    }

    [Fact]
    public async Task Checkpoint_IsPersistedDuringRun_AndClearedOnSuccess()
    {
        WriteTasks("- [ ] Checkpointed task");
        ExecutionCheckpoint? seenDuringRun = null;
        JsonCheckpointStore? storeRef = null;

        var (orchestrator, _, _, store, _) = Build((t, a) =>
        {
            // capture checkpoint state mid-run (synchronously via the store's file)
            seenDuringRun = storeRef!.LoadAsync().GetAwaiter().GetResult();
            return Success(t, a);
        });
        storeRef = store;

        await orchestrator.RunAllAsync(CancellationToken.None);

        seenDuringRun.Should().NotBeNull("the checkpoint must be written BEFORE the agent works");
        seenDuringRun!.TaskLine.Should().Be(1);
        seenDuringRun.State.Should().Be(TaskWorkflowState.Running);
        seenDuringRun.Prompt.Should().NotBeNullOrEmpty("the prompt must be persisted for crash recovery");
        (await store.LoadAsync()).Should().BeNull("checkpoint must be cleared once the task completes");
    }

    [Fact]
    public async Task CrashRecovery_ResumesSameTask_AtCheckpointedAttempt()
    {
        WriteTasks("- [/] Crashed mid-task");
        var (orchestrator, pipeline, _, store, options) = Build(Success, o => o.Retry.MaxRetries = 5);

        // Simulate a previous run that crashed during attempt 3 of this task.
        var crashed = ExecutionCheckpoint.Start(1, "Crashed mid-task", options.Model) with
        {
            State = TaskWorkflowState.Running,
            Attempt = 3,
            History =
            [
                new AttemptRecord(1, DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow.AddMinutes(-9), false, FailureKind.MarkerMissing, "no marker"),
                new AttemptRecord(2, DateTimeOffset.UtcNow.AddMinutes(-8), DateTimeOffset.UtcNow.AddMinutes(-7), false, FailureKind.BuildFailed, "build broke"),
            ],
        };
        await store.SaveAsync(crashed);
        await store.SaveSnapshotAsync(new WorkspaceSnapshot(new Dictionary<string, FileSnapshot>(), DateTime.UtcNow));

        await orchestrator.RunAllAsync(CancellationToken.None);

        pipeline.Calls.Should().ContainSingle();
        pipeline.Calls[0].Line.Should().Be(1, "recovery must resume the SAME task");
        pipeline.Calls[0].Attempt.Should().Be(3, "recovery must resume at the checkpointed attempt, not restart at 1");
        ReadTasks()[0].Should().StartWith("- [x]");
    }

    [Fact]
    public async Task RetryFailed_StartsFresh_NotFromStaleFailedCheckpoint()
    {
        // Regression: --retry-failed once resumed the failed run's checkpoint, reusing a
        // stale workspace baseline and the old attempt counter.
        WriteTasks("- [!] Previously failed");
        var (orchestrator, pipeline, _, store, options) = Build(Success, o => o.RetryFailedTasks = true);

        var staleCheckpoint = ExecutionCheckpoint.Start(1, "Previously failed", options.Model) with
        {
            State = TaskWorkflowState.Failed,
            Attempt = 4,
            History =
            [
                new AttemptRecord(4, DateTimeOffset.UtcNow.AddMinutes(-3), DateTimeOffset.UtcNow, false, FailureKind.MarkerMissing, "old"),
            ],
        };
        await store.SaveAsync(staleCheckpoint);
        await store.SaveSnapshotAsync(new WorkspaceSnapshot(new Dictionary<string, FileSnapshot>(), DateTime.UtcNow));

        await orchestrator.RunAllAsync(CancellationToken.None);

        pipeline.Calls.Should().ContainSingle();
        pipeline.Calls[0].Attempt.Should().Be(1, "a re-attempt of a failed task must start fresh at attempt 1");
        ReadTasks()[0].Should().StartWith("- [x]");
    }

    [Fact]
    public async Task FailedCheckpoint_IsNeverResumed_EvenWithoutRetryFlag()
    {
        // A Failed-state checkpoint is diagnostic only. If the user manually resets the
        // task to [ ] and re-runs, execution must start fresh, not resume attempt 4.
        WriteTasks("- [ ] Manually reset task");
        var (orchestrator, pipeline, _, store, options) = Build(Success, o => o.Retry.MaxRetries = 5);

        var staleCheckpoint = ExecutionCheckpoint.Start(1, "Manually reset task", options.Model) with
        {
            State = TaskWorkflowState.Failed,
            Attempt = 4,
        };
        await store.SaveAsync(staleCheckpoint);
        await store.SaveSnapshotAsync(new WorkspaceSnapshot(new Dictionary<string, FileSnapshot>(), DateTime.UtcNow));

        await orchestrator.RunAllAsync(CancellationToken.None);

        pipeline.Calls.Should().ContainSingle();
        pipeline.Calls[0].Attempt.Should().Be(1);
    }

    [Fact]
    public async Task FailedRun_KeepsCheckpointWithHistory_ForDiagnosis()
    {
        WriteTasks("- [ ] Always fails");
        var (orchestrator, _, _, store, _) = Build((t, a) => Failure(t, a, FailureKind.BuildFailed),
            o => o.Retry.MaxRetries = 1);

        await orchestrator.RunAllAsync(CancellationToken.None);

        var checkpoint = await store.LoadAsync();
        checkpoint.Should().NotBeNull("the checkpoint must survive failure for the halt report / --retry-failed");
        checkpoint!.State.Should().Be(TaskWorkflowState.Failed);
        checkpoint.History.Should().HaveCount(2, "both attempts must be recorded");
        checkpoint.History.Should().OnlyContain(h => h.Failure == FailureKind.BuildFailed);
    }

    [Fact]
    public async Task DryRun_ExecutesNothing()
    {
        WriteTasks("- [ ] Task one");
        var (orchestrator, pipeline, _, _, _) = Build(Success, o => o.DryRun = true);

        await orchestrator.RunAllAsync(CancellationToken.None);

        pipeline.Calls.Should().BeEmpty();
        ReadTasks()[0].Should().StartWith("- [ ]");
    }
}
