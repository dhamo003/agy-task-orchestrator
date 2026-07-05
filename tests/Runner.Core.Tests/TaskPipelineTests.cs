using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Core.Models;
using AntigravityTaskRunner.Core.Pipeline;
using AntigravityTaskRunner.Core.Progress;
using AntigravityTaskRunner.Core.Verification;
using AntigravityTaskRunner.Terminal.Build;
using AntigravityTaskRunner.Terminal.Detection;
using AntigravityTaskRunner.Terminal.Sessions;
using AntigravityTaskRunner.Terminal.Workspace;
using Runner.Markdown.Models;
using Runner.Logging;
using TaskStatus = Runner.Markdown.Models.TaskStatus;

namespace AntigravityTaskRunner.Core.Tests;

/// <summary>
/// Covers the per-attempt pipeline: verification gating (marker, changes, meaningful
/// diff), build/test validation gating, capacity-limit detection, and timeouts.
/// </summary>
public class TaskPipelineTests
{
    private static readonly TaskItem Task1 = new(1, "- [ ] Task 1", "Task 1", TaskStatus.NotStarted, null, 0);
    private static readonly WorkspaceSnapshot EmptySnapshot =
        new(new Dictionary<string, FileSnapshot>(), DateTime.UtcNow);

    private sealed class FakeSessionRunner : IAgentSessionRunner
    {
        private readonly AgentRunResult _result;
        public FakeSessionRunner(AgentRunResult result) => _result = result;
        public Task<AgentRunResult> RunAsync(TaskLogScope scope, string prompt,
            Action<string>? reportStatus = null, CancellationToken token = default) =>
            Task.FromResult(_result);
    }

    private sealed class FakeBuildValidator : IBuildValidator
    {
        private readonly BuildValidationResult _result;
        public int Calls { get; private set; }
        public FakeBuildValidator(BuildValidationResult result) => _result = result;

        public IReadOnlySet<string>? TestFailureBaseline { get; set; }

        public Task<TestBaseline> CaptureTestBaselineAsync(CancellationToken token = default) =>
            Task.FromResult(TestBaseline.Empty);

        public Task<BuildValidationResult> ValidateAsync(CancellationToken token = default)
        {
            Calls++;
            return Task.FromResult(_result);
        }
    }

    private static AgentRunResult CompletedRun(string output = "TASK_COMPLETED") =>
        new(output, MarkerDetected: true, MarkerSuccess: true, MarkerMessage: null, ExitCode: null, TimedOut: false);

    private static BuildValidationResult BuildPassed() =>
        new(true, false, [new BuildStageResult("build", "dotnet build", true, false, 0, "", TimeSpan.Zero)], "ok");

    private static BuildValidationResult BuildFailed(string stageName, string output) =>
        new(false, false,
            [new BuildStageResult(stageName, $"dotnet {stageName}", false, false, 1, output, TimeSpan.Zero)],
            $"Stage '{stageName}' failed (exit 1).");

    private static WorkspaceChangeSet Changes(params FileChange[] changes) => new(changes);

    private static (TaskPipeline Pipeline, FakeBuildValidator Build) BuildPipeline(
        AgentRunResult runResult,
        WorkspaceChangeSet changeSet,
        BuildValidationResult? buildResult = null)
    {
        var options = new RunnerOptions { TasksFile = "tasks.md", WorkspacePath = "." };
        var ioptions = Microsoft.Extensions.Options.Options.Create(options);

        var analyzerMock = new Mock<IWorkspaceAnalyzer>();
        analyzerMock.Setup(a => a.TakeSnapshotAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EmptySnapshot);
        analyzerMock.Setup(a => a.GetChangeSet(It.IsAny<WorkspaceSnapshot>(), It.IsAny<WorkspaceSnapshot>()))
            .Returns(changeSet);

        var build = new FakeBuildValidator(buildResult ?? BuildPassed());

        var pipeline = new TaskPipeline(
            new FakeSessionRunner(runResult),
            analyzerMock.Object,
            new CompletionVerifier(ioptions),
            build,
            new PatternLimitDetector(ioptions, new Mock<ILogger<PatternLimitDetector>>().Object),
            new ProgressTracker(),
            new Mock<ITaskLogger>().Object);

        return (pipeline, build);
    }

    [Fact]
    public async Task Succeeds_WhenMarkerAndMeaningfulChangeAndBuildPass()
    {
        var (pipeline, build) = BuildPipeline(
            CompletedRun(),
            Changes(new FileChange("src/Foo.cs", FileChangeKind.Modified, IsMeaningful: true)));

        var result = await pipeline.ExecuteAsync(Task1, EmptySnapshot, "prompt", 1);

        result.IsSuccess.Should().BeTrue();
        result.Failure.Should().Be(FailureKind.None);
        result.Verification!.Passed.Should().BeTrue();
        build.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Fails_MarkerMissing_WhenAgentNeverPrintsCompletion()
    {
        var (pipeline, build) = BuildPipeline(
            new AgentRunResult("did some things", false, false, null, null, false),
            Changes(new FileChange("src/Foo.cs", FileChangeKind.Modified, IsMeaningful: true)));

        var result = await pipeline.ExecuteAsync(Task1, EmptySnapshot, "prompt", 1);

        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(FailureKind.MarkerMissing);
        build.Calls.Should().Be(0, "build validation must not run when verification already failed");
    }

    [Fact]
    public async Task Fails_NoChanges_WhenMarkerPresentButNothingChanged()
    {
        var (pipeline, _) = BuildPipeline(CompletedRun(), WorkspaceChangeSet.Empty);

        var result = await pipeline.ExecuteAsync(Task1, EmptySnapshot, "prompt", 1);

        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(FailureKind.NoChanges);
    }

    [Fact]
    public async Task Fails_NoMeaningfulChanges_WhenOnlyDocsOrFormattingChanged()
    {
        var (pipeline, _) = BuildPipeline(
            CompletedRun(),
            Changes(
                new FileChange("README.md", FileChangeKind.Modified, IsMeaningful: false),
                new FileChange("src/Foo.cs", FileChangeKind.Modified, IsMeaningful: false)));

        var result = await pipeline.ExecuteAsync(Task1, EmptySnapshot, "prompt", 1);

        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(FailureKind.NoMeaningfulChanges);
        result.Verification!.FailedChecks.Should().Contain(c => c.Name == "MeaningfulImplementationDiff");
    }

    [Fact]
    public async Task Fails_AgentReportedFailure_OnTaskFailedMarker()
    {
        var (pipeline, _) = BuildPipeline(
            new AgentRunResult("TASK_FAILED: cannot", true, false, "TASK_FAILED: cannot", null, false),
            Changes(new FileChange("src/Foo.cs", FileChangeKind.Modified, IsMeaningful: true)));

        var result = await pipeline.ExecuteAsync(Task1, EmptySnapshot, "prompt", 1);

        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(FailureKind.AgentReportedFailure);
    }

    [Fact]
    public async Task Fails_BuildFailed_AndCarriesBuildOutputForRetryContext()
    {
        var (pipeline, _) = BuildPipeline(
            CompletedRun(),
            Changes(new FileChange("src/Foo.cs", FileChangeKind.Modified, IsMeaningful: true)),
            BuildFailed("build", "Foo.cs(10,5): error CS0103: The name 'bar' does not exist"));

        var result = await pipeline.ExecuteAsync(Task1, EmptySnapshot, "prompt", 1);

        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(FailureKind.BuildFailed);
        result.Build!.FirstFailure!.Output.Should().Contain("CS0103");

        // The retry context must surface the compiler error to the next attempt.
        var guidance = new Retry.RetryContext(2, result).BuildGuidance();
        guidance.Should().Contain("CS0103");
    }

    [Fact]
    public async Task Fails_TestsFailed_WhenTestStageFails()
    {
        var (pipeline, _) = BuildPipeline(
            CompletedRun(),
            Changes(new FileChange("src/Foo.cs", FileChangeKind.Modified, IsMeaningful: true)),
            BuildFailed("test", "Failed! - 2 tests failed"));

        var result = await pipeline.ExecuteAsync(Task1, EmptySnapshot, "prompt", 1);

        result.Failure.Should().Be(FailureKind.TestsFailed);
    }

    [Fact]
    public async Task DetectsCapacityLimit_BeforeAnyVerification()
    {
        var (pipeline, build) = BuildPipeline(
            new AgentRunResult("error: rate limit exceeded, please wait", false, false, null, null, false),
            WorkspaceChangeSet.Empty);

        var result = await pipeline.ExecuteAsync(Task1, EmptySnapshot, "prompt", 1);

        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(FailureKind.CapacityLimit);
        result.Limit.Should().NotBeNull();
        result.Limit!.MatchedPattern.Should().Be("rate limit exceeded");
        build.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Fails_Timeout_WhenSessionTimedOut()
    {
        var (pipeline, _) = BuildPipeline(
            new AgentRunResult("partial output…", false, false, null, null, TimedOut: true),
            WorkspaceChangeSet.Empty);

        var result = await pipeline.ExecuteAsync(Task1, EmptySnapshot, "prompt", 1);

        result.Failure.Should().Be(FailureKind.Timeout);
    }

    [Fact]
    public async Task Fails_SessionFailure_WhenCliNeverBecameReady()
    {
        var (pipeline, _) = BuildPipeline(
            new AgentRunResult("", false, false, null, null, false,
                FailureDetail: "Timed out waiting for the agent CLI to become ready."),
            WorkspaceChangeSet.Empty);

        var result = await pipeline.ExecuteAsync(Task1, EmptySnapshot, "prompt", 1);

        result.Failure.Should().Be(FailureKind.SessionFailure);
        result.ErrorMessage.Should().Contain("become ready");
    }

    [Fact]
    public async Task OneShot_NonZeroExit_FailsVerification()
    {
        var (pipeline, _) = BuildPipeline(
            new AgentRunResult("boom", false, false, null, ExitCode: 1, TimedOut: false),
            Changes(new FileChange("src/Foo.cs", FileChangeKind.Modified, IsMeaningful: true)));

        var result = await pipeline.ExecuteAsync(Task1, EmptySnapshot, "prompt", 1);

        result.IsSuccess.Should().BeFalse();
        result.Verification!.FailedChecks.Should().Contain(c => c.Detail.Contains("non-zero code 1"));
    }
}
