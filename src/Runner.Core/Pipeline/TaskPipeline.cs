using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AntigravityTaskRunner.Core.Models;
using AntigravityTaskRunner.Core.Progress;
using AntigravityTaskRunner.Core.Verification;
using AntigravityTaskRunner.Terminal.Build;
using AntigravityTaskRunner.Terminal.Detection;
using AntigravityTaskRunner.Terminal.Sessions;
using AntigravityTaskRunner.Terminal.Workspace;
using Runner.Logging;
using Runner.Markdown.Models;

namespace AntigravityTaskRunner.Core.Pipeline;

/// <summary>
/// Orchestrates one attempt of a single task. Stages:
/// 1. Run a fresh, isolated agent session (torn down before this method returns).
/// 2. Scan the output for capacity limits (token/rate/quota/context) — these pause, not fail.
/// 3. Verify the workspace: categorized change set + meaningful-implementation-diff check.
/// 4. Run build &amp; test validation (dotnet restore/build/test) — only when 3 passed.
/// A task attempt succeeds only when every stage passes.
/// </summary>
public class TaskPipeline : ITaskPipeline
{
    private readonly IAgentSessionRunner _sessionRunner;
    private readonly IWorkspaceAnalyzer _workspaceAnalyzer;
    private readonly ICompletionVerifier _verifier;
    private readonly IBuildValidator _buildValidator;
    private readonly ILimitDetector _limitDetector;
    private readonly IProgressTracker _progress;
    private readonly ITaskLogger _logger;

    public TaskPipeline(
        IAgentSessionRunner sessionRunner,
        IWorkspaceAnalyzer workspaceAnalyzer,
        ICompletionVerifier verifier,
        IBuildValidator buildValidator,
        ILimitDetector limitDetector,
        IProgressTracker progress,
        ITaskLogger logger)
    {
        _sessionRunner = sessionRunner;
        _workspaceAnalyzer = workspaceAnalyzer;
        _verifier = verifier;
        _buildValidator = buildValidator;
        _limitDetector = limitDetector;
        _progress = progress;
        _logger = logger;
    }

    public async Task<TaskExecutionResult> ExecuteAsync(
        TaskItem task,
        WorkspaceSnapshot initialSnapshot,
        string prompt,
        int attempt,
        CancellationToken token = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var scope = new TaskLogScope($"T-{task.LineNumber}", task.DisplayText, attempt);
        _logger.LogInfo(scope, "Starting task pipeline attempt...");

        try
        {
            // Stage 1: fresh agent session (runner guarantees deterministic teardown).
            var runResult = await _sessionRunner.RunAsync(
                scope, prompt,
                status => _progress.ReportStatus(task, status),
                token);

            // Stage 2: capacity-limit detection. Checked FIRST so a rate-limited attempt
            // pauses instead of being misclassified as a normal failure.
            var limit = _limitDetector.Detect(runResult.Output);
            if (limit is not null)
            {
                stopwatch.Stop();
                _logger.LogWarning(scope, $"Capacity limit detected: {limit.MatchedText}");
                return new TaskExecutionResult(task, false, stopwatch.Elapsed,
                    $"Capacity limit detected ({limit.MatchedPattern}): {limit.MatchedText}",
                    attempt, FailureKind.CapacityLimit, Limit: limit);
            }

            // Session-level failures.
            if (runResult.FailureDetail is not null)
            {
                stopwatch.Stop();
                return new TaskExecutionResult(task, false, stopwatch.Elapsed,
                    runResult.FailureDetail, attempt, FailureKind.SessionFailure);
            }

            if (runResult.TimedOut)
            {
                stopwatch.Stop();
                return new TaskExecutionResult(task, false, stopwatch.Elapsed,
                    "Task timed out before the agent finished.", attempt, FailureKind.Timeout);
            }

            // Stage 3: workspace verification.
            _progress.ReportStatus(task, "Verifying workspace changes");
            _logger.LogInfo(scope, "[workflow] Verification started");
            var finalSnapshot = await _workspaceAnalyzer.TakeSnapshotAsync(token);
            var changeSet = _workspaceAnalyzer.GetChangeSet(initialSnapshot, finalSnapshot);
            var verification = _verifier.Verify(runResult, changeSet);
            _logger.LogInfo(scope, verification.Describe());

            if (!verification.Passed)
            {
                stopwatch.Stop();
                var kind = ClassifyVerificationFailure(runResult, changeSet);
                var reason = string.Join("; ", System.Linq.Enumerable.Select(
                    verification.FailedChecks, c => c.Detail));
                return new TaskExecutionResult(task, false, stopwatch.Elapsed, reason, attempt,
                    kind, Verification: verification);
            }

            // Stage 4: build & test validation.
            _progress.ReportStatus(task, "Running build & test validation");
            _logger.LogInfo(scope, "[workflow] Build validation started");
            var build = await _buildValidator.ValidateAsync(token);
            foreach (var stage in build.Stages)
            {
                _logger.LogInfo(scope,
                    $"[build] {stage.Name}: {(stage.Skipped ? $"skipped ({stage.SkipReason})" : stage.Success ? "passed" : $"FAILED (exit {stage.ExitCode})")} in {stage.Duration.TotalSeconds:F1}s");
            }

            if (!build.Success)
            {
                stopwatch.Stop();
                var failedStage = build.FirstFailure;
                var kind = failedStage is not null &&
                           failedStage.Name.Contains("test", StringComparison.OrdinalIgnoreCase)
                    ? FailureKind.TestsFailed
                    : FailureKind.BuildFailed;
                return new TaskExecutionResult(task, false, stopwatch.Elapsed,
                    build.Summary, attempt, kind, Verification: verification, Build: build);
            }

            stopwatch.Stop();
            _logger.LogInfo(scope, "All verification and build checks passed.");
            return new TaskExecutionResult(task, true, stopwatch.Elapsed, null, attempt,
                FailureKind.None, Verification: verification, Build: build);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(scope, "Pipeline execution failed with exception", ex);
            return new TaskExecutionResult(task, false, stopwatch.Elapsed, ex.Message, attempt,
                FailureKind.Exception);
        }
    }

    private static FailureKind ClassifyVerificationFailure(AgentRunResult runResult, WorkspaceChangeSet changeSet)
    {
        if (runResult.MarkerDetected && !runResult.MarkerSuccess)
        {
            return FailureKind.AgentReportedFailure;
        }

        if (runResult.ExitCode is int exit && exit != 0)
        {
            return FailureKind.SessionFailure;
        }

        if (!(runResult.MarkerDetected && runResult.MarkerSuccess))
        {
            return FailureKind.MarkerMissing;
        }

        if (!changeSet.HasAnyChanges)
        {
            return FailureKind.NoChanges;
        }

        return FailureKind.NoMeaningfulChanges;
    }
}
