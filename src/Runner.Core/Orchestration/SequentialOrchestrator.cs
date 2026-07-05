using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Core.Checkpointing;
using AntigravityTaskRunner.Core.Models;
using AntigravityTaskRunner.Core.Pipeline;
using AntigravityTaskRunner.Core.Progress;
using AntigravityTaskRunner.Core.Prompts;
using AntigravityTaskRunner.Core.Retry;
using AntigravityTaskRunner.Core.Workflow;
using AntigravityTaskRunner.Terminal.Build;
using AntigravityTaskRunner.Terminal.Workspace;
using Runner.Markdown.Parser;
using Runner.Markdown.Writer;
using Runner.Markdown.Models;
using Runner.Logging;
using TaskStatus = Runner.Markdown.Models.TaskStatus;

namespace AntigravityTaskRunner.Core.Orchestration;

/// <summary>
/// Strictly sequential, state-machine-driven orchestrator.
///
/// Guarantees:
/// - Exactly one task is active at any time; the next task cannot start until the
///   current one is fully verified, its session torn down, and its state persisted.
/// - Tasks are never skipped: a permanent failure HALTS the pipeline with a full
///   report instead of moving on.
/// - Capacity limits pause execution (checkpointed) and resume on the same task.
/// - Every workflow transition is checkpointed for crash recovery.
/// </summary>
public class SequentialOrchestrator : ITaskOrchestrator
{
    private readonly ITaskParser _taskParser;
    private readonly ITaskWriter _taskWriter;
    private readonly ITaskPipeline _taskPipeline;
    private readonly IRetryPolicy _retryPolicy;
    private readonly IProgressTracker _progressTracker;
    private readonly IWorkspaceAnalyzer _workspaceAnalyzer;
    private readonly IPromptTemplateEngine _promptEngine;
    private readonly ICheckpointStore _checkpointStore;
    private readonly IBuildValidator _buildValidator;
    private readonly ITaskLogger _logger;
    private readonly RunnerOptions _options;
    private bool _testBaselineReady;

    public SequentialOrchestrator(
        ITaskParser taskParser,
        ITaskWriter taskWriter,
        ITaskPipeline taskPipeline,
        IRetryPolicy retryPolicy,
        IProgressTracker progressTracker,
        IWorkspaceAnalyzer workspaceAnalyzer,
        IPromptTemplateEngine promptEngine,
        ICheckpointStore checkpointStore,
        IBuildValidator buildValidator,
        ITaskLogger logger,
        IOptions<RunnerOptions> options)
    {
        _taskParser = taskParser;
        _taskWriter = taskWriter;
        _taskPipeline = taskPipeline;
        _retryPolicy = retryPolicy;
        _progressTracker = progressTracker;
        _workspaceAnalyzer = workspaceAnalyzer;
        _promptEngine = promptEngine;
        _checkpointStore = checkpointStore;
        _buildValidator = buildValidator;
        _logger = logger;
        _options = options.Value;
    }

    public async Task RunAllAsync(CancellationToken token = default)
    {
        var logScope = new TaskLogScope("Orchestrator", "RunAllSequential", 0);
        _logger.LogInfo(logScope, "Starting sequential orchestration...");

        if (_options.DryRun)
        {
            await RunDryAsync(logScope, token);
            return;
        }

        // Crash recovery: announce a resumable checkpoint if one exists.
        var existing = await _checkpointStore.LoadAsync(token);
        if (existing is not null && existing.State is not TaskWorkflowState.Completed)
        {
            _logger.LogInfo(logScope,
                $"Found checkpoint for task line {existing.TaskLine} (state {existing.State}, attempt {existing.Attempt}). " +
                "Execution will resume on that exact task.");
        }

        // Test-failure baseline: captured ONCE per run, before any agent work, so that
        // pre-existing red tests don't permanently block every task (only regressions do).
        await EnsureTestBaselineAsync(existing, logScope, token);

        // Guards against re-processing the same line twice in one run, which would
        // violate strict one-by-one execution.
        var attemptedLines = new HashSet<int>();

        while (!token.IsCancellationRequested)
        {
            var phases = await _taskParser.ParseAsync(_options.TasksFile, token);
            var nextTask = _taskParser.GetNextTask(phases);

            if (nextTask == null)
            {
                _logger.LogInfo(logScope, "No pending tasks found. Orchestration complete.");
                await _checkpointStore.ClearAsync(token);
                break;
            }

            // Fail-stop: a task already marked failed blocks the pipeline. It is never
            // skipped. It can be re-attempted explicitly via --retry-failed.
            if (nextTask.Status == TaskStatus.Failed)
            {
                if (_options.RetryFailedTasks)
                {
                    _logger.LogInfo(logScope,
                        $"Task on line {nextTask.LineNumber} previously failed; --retry-failed set, re-attempting it.");

                    // A re-attempt starts FRESH: the failed run's checkpoint (kept for the
                    // halt report) and its stale workspace baseline must not be resumed.
                    await _checkpointStore.ClearAsync(token);
                }
                else
                {
                    _logger.LogError(logScope,
                        $"Task on line {nextTask.LineNumber} ('{nextTask.DisplayText}') is marked failed [!]. " +
                        "The pipeline will not continue past an unresolved failure. " +
                        "Fix/complete it manually, or re-run with --retry-failed.");
                    var blockedResult = new TaskExecutionResult(nextTask, false, TimeSpan.Zero,
                        "Task is marked failed from a previous run; pipeline halted (tasks are never skipped).",
                        0, FailureKind.AgentReportedFailure);
                    var history = (await _checkpointStore.LoadAsync(token))?.History ?? [];
                    _progressTracker.ReportPipelineHalted(new PipelineHaltReport(blockedResult, history));
                    break;
                }
            }

            if (!attemptedLines.Add(nextTask.LineNumber))
            {
                _logger.LogWarning(logScope,
                    $"Task on line {nextTask.LineNumber} was already attempted this run; stopping to avoid a re-processing loop.");
                break;
            }

            // Awaited: the next task cannot start until RunSingleAsync (including
            // deterministic session teardown and verification) has fully completed.
            var result = await RunSingleAsync(nextTask, token);

            if (!result.IsSuccess)
            {
                // FAIL-STOP: never continue to the next task past a failure.
                var checkpoint = await _checkpointStore.LoadAsync(token);
                var report = new PipelineHaltReport(result, checkpoint?.History ?? []);
                _logger.LogError(logScope, report.Describe());
                _progressTracker.ReportPipelineHalted(report);
                break;
            }
        }
    }

    public async Task<TaskExecutionResult> RunSingleAsync(TaskItem task, CancellationToken token = default)
    {
        var scope = new TaskLogScope($"T-{task.LineNumber}", task.DisplayText, 0);
        var stateMachine = new TaskStateMachine(_logger, scope);
        _progressTracker.ReportTaskStarted(task);
        _progressTracker.ReportStatus(task, "Pending — preparing task");

        // ---- Checkpoint / crash recovery -------------------------------------------------
        // If a checkpoint exists for this exact task, resume from it: restore the retry
        // count, pause count, history, and — critically — the ORIGINAL pre-task workspace
        // snapshot so change verification still compares against the true baseline.
        var checkpoint = await _checkpointStore.LoadAsync(token);
        WorkspaceSnapshot? baseline = null;
        RetryContext? initialContext = null;

        // Failed-state checkpoints exist only for diagnostics/halt reports; resuming one
        // would reuse a stale baseline. Only live states (Pending/Running/Verifying/Paused)
        // are resumable.
        if (checkpoint is not null && checkpoint.State is TaskWorkflowState.Failed or TaskWorkflowState.Completed)
        {
            checkpoint = null;
        }

        if (checkpoint is not null && checkpoint.Matches(task.LineNumber, task.DisplayText))
        {
            baseline = await _checkpointStore.LoadSnapshotAsync(token);
            if (baseline is not null)
            {
                initialContext = new RetryContext(Math.Max(1, checkpoint.Attempt), null);
                _logger.LogInfo(scope,
                    $"Resuming task from checkpoint: state {checkpoint.State}, attempt {checkpoint.Attempt}, " +
                    $"{checkpoint.History.Count} prior attempt(s) recorded.");
            }
            else
            {
                _logger.LogWarning(scope, "Checkpoint found but workspace snapshot missing; starting the task fresh.");
                checkpoint = null;
            }
        }
        else
        {
            checkpoint = null;
        }

        if (checkpoint is null)
        {
            // Fresh task: capture the baseline BEFORE any attempt, and persist both the
            // checkpoint and the snapshot before any agent work begins.
            baseline = await _workspaceAnalyzer.TakeSnapshotAsync(token);
            checkpoint = ExecutionCheckpoint.Start(task.LineNumber, task.DisplayText, _options.Model) with
            {
                BaselineFailedTests = _buildValidator.TestFailureBaseline?.ToList(),
            };
            await _checkpointStore.SaveSnapshotAsync(baseline, token);
            await _checkpointStore.SaveAsync(checkpoint, token);
        }
        else if (_buildValidator.TestFailureBaseline is null && checkpoint.BaselineFailedTests is not null)
        {
            // Crash recovery: restore the pre-task test baseline; recapturing now would
            // wrongly include failures the agent may have introduced mid-task.
            _buildValidator.TestFailureBaseline =
                new HashSet<string>(checkpoint.BaselineFailedTests, StringComparer.Ordinal);
            _testBaselineReady = true;
        }

        // Mark in-progress in the checklist.
        await _taskWriter.UpdateStatusAsync(_options.TasksFile, task, TaskStatus.InProgress, null, token);

        var history = new List<AttemptRecord>(checkpoint.History);
        var attemptStartedAt = DateTimeOffset.UtcNow;

        // ---- Attempt loop (retry policy owns backoff & capacity pauses) ------------------
        var result = await _retryPolicy.ExecuteAsync(
            attempt: async (context, ct) =>
            {
                attemptStartedAt = DateTimeOffset.UtcNow;

                // FSM: Pending→Running on first attempt; Verifying/Paused→Running on retry/resume.
                if (stateMachine.State == TaskWorkflowState.Pending)
                {
                    stateMachine.TransitionTo(TaskWorkflowState.Running, $"attempt {context.Attempt}");
                }
                else if (stateMachine.State is TaskWorkflowState.Verifying or TaskWorkflowState.Paused)
                {
                    stateMachine.TransitionTo(TaskWorkflowState.Running,
                        stateMachine.State == TaskWorkflowState.Paused
                            ? "resumed after capacity pause"
                            : $"retry — attempt {context.Attempt}");
                }

                _progressTracker.ReportStatus(task,
                    context.IsRetry ? $"Running (attempt {context.Attempt}, with failure context)" : "Running (fresh AI session)");

                var prompt = await _promptEngine.BuildPromptAsync(task, baseline!, context, ct);

                checkpoint = checkpoint! with
                {
                    State = TaskWorkflowState.Running,
                    Attempt = context.Attempt,
                    Prompt = prompt,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                };
                await _checkpointStore.SaveAsync(checkpoint, ct);

                return await _taskPipeline.ExecuteAsync(task, baseline!, prompt, context.Attempt, ct);
            },
            onAttemptCompleted: async (attemptResult, context) =>
            {
                history.Add(new AttemptRecord(
                    context.Attempt, attemptStartedAt, DateTimeOffset.UtcNow,
                    attemptResult.IsSuccess, attemptResult.Failure, attemptResult.ErrorMessage));

                // FSM per attempt outcome.
                if (attemptResult.IsCapacityLimit)
                {
                    stateMachine.TransitionTo(TaskWorkflowState.Paused,
                        $"capacity limit: {attemptResult.Limit?.MatchedPattern}");
                    _progressTracker.ReportStatus(task, "Paused — capacity limit; state saved, will resume this task");
                }
                else
                {
                    stateMachine.TransitionTo(TaskWorkflowState.Verifying,
                        attemptResult.IsSuccess ? "attempt verified" : $"attempt failed: {attemptResult.Failure}");
                }

                var modifiedFiles = attemptResult.Verification?.ChangeSet.Changes
                    .Select(c => $"{c.Kind}:{c.Path}").ToList() ?? [];

                checkpoint = checkpoint! with
                {
                    State = stateMachine.State,
                    PauseCount = attemptResult.IsCapacityLimit ? checkpoint!.PauseCount + 1 : checkpoint!.PauseCount,
                    ModifiedFiles = modifiedFiles,
                    History = history.ToList(),
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                };
                await _checkpointStore.SaveAsync(checkpoint, token);
            },
            initialContext: initialContext,
            token: token);

        // ---- Terminal transition ---------------------------------------------------------
        if (result.IsSuccess)
        {
            stateMachine.TransitionTo(TaskWorkflowState.Completed, "all checks passed");
            await _taskWriter.UpdateStatusAsync(_options.TasksFile, task, TaskStatus.Completed, null, token);
            await _checkpointStore.ClearAsync(token);
            _progressTracker.ReportStatus(task, "Completed");
        }
        else if (!stateMachine.IsTerminal)
        {
            stateMachine.TransitionTo(TaskWorkflowState.Failed, result.ErrorMessage);
            await _taskWriter.UpdateStatusAsync(_options.TasksFile, task, TaskStatus.Failed, result.ErrorMessage, token);

            // Keep the checkpoint (state=Failed) so the halt report and any --retry-failed
            // run still has the attempt history.
            checkpoint = checkpoint! with
            {
                State = TaskWorkflowState.Failed,
                History = history.ToList(),
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };
            await _checkpointStore.SaveAsync(checkpoint, token);
            _progressTracker.ReportStatus(task, $"Failed — {result.Failure}");
        }

        _progressTracker.ReportTaskCompleted(task, result.IsSuccess, result.Duration);
        return result;
    }

    public Task StopAsync()
    {
        // Graceful shutdown is handled via cancellation tokens from the host; the
        // checkpoint written after every transition makes the run resumable.
        return Task.CompletedTask;
    }

    private async Task EnsureTestBaselineAsync(
        ExecutionCheckpoint? resumableCheckpoint, TaskLogScope logScope, CancellationToken token)
    {
        if (_testBaselineReady
            || !_options.Build.Enabled
            || !_options.Build.RunTests
            || !_options.Build.FailOnlyOnNewTestFailures)
        {
            return;
        }

        // Crash recovery: the checkpoint carries the baseline captured before the agent
        // ever ran; recapturing now would be polluted by the agent's partial work.
        if (resumableCheckpoint?.BaselineFailedTests is { } persisted
            && resumableCheckpoint.State is not TaskWorkflowState.Failed and not TaskWorkflowState.Completed)
        {
            _buildValidator.TestFailureBaseline = new HashSet<string>(persisted, StringComparer.Ordinal);
            _testBaselineReady = true;
            _logger.LogInfo(logScope,
                $"Restored test baseline from checkpoint ({persisted.Count} pre-existing failing test(s)).");
            return;
        }

        _logger.LogInfo(logScope, "[workflow] Capturing test baseline (pre-existing failures will not block tasks)...");
        var testBaseline = await _buildValidator.CaptureTestBaselineAsync(token);
        _buildValidator.TestFailureBaseline = testBaseline.FailedTests;
        _testBaselineReady = true;
        _logger.LogInfo(logScope, testBaseline.Summary);

        if (!testBaseline.BuildSucceeded)
        {
            _logger.LogWarning(logScope,
                "The workspace does not build BEFORE any task ran. Tasks will halt on build validation until this is fixed.");
        }
    }

    private async Task RunDryAsync(TaskLogScope logScope, CancellationToken token)
    {
        _logger.LogInfo(logScope, "Dry-run mode enabled. Tasks will be parsed but not executed.");
        var parsedPhases = await _taskParser.ParseAsync(_options.TasksFile, token);
        foreach (var phase in parsedPhases)
        {
            foreach (var task in phase.Tasks)
            {
                _logger.LogInfo(logScope, $"Pending Task: {task.DisplayText}");
            }
        }
    }
}
