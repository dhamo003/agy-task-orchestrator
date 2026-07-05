using System.Collections.Generic;
using System.Text;
using AntigravityTaskRunner.Core.Checkpointing;

namespace AntigravityTaskRunner.Core.Models;

/// <summary>
/// Everything the operator needs when the pipeline stops permanently: the final failed
/// result, the complete retry history, and a suggested next action.
/// </summary>
public sealed record PipelineHaltReport(
    TaskExecutionResult Result,
    IReadOnlyList<AttemptRecord> History)
{
    public string SuggestedNextAction => Result.Failure switch
    {
        FailureKind.BuildFailed =>
            "Fix the compiler errors shown in the build output (or revert the last attempt's changes), then re-run. The task will resume — it was not skipped.",
        FailureKind.TestsFailed =>
            "Inspect the failing tests in the output above, fix or revert the changes, then re-run.",
        FailureKind.CapacityLimit =>
            "The model capacity limit persisted through every pause. Wait for quota to recover (or switch model in appsettings.json), then re-run — execution resumes on this exact task.",
        FailureKind.Timeout =>
            "Increase Runner:Timeout:TaskTimeoutMinutes or split this task into smaller checklist items, then re-run.",
        FailureKind.NoChanges or FailureKind.NoMeaningfulChanges =>
            "The agent never produced real code changes. Clarify the task wording in the checklist, or implement it manually and mark it [x]. Re-run with --retry-failed to try again.",
        FailureKind.SessionFailure =>
            "The agent CLI could not be driven (not installed / not authenticated / wrong AgentCommand?). Verify the CLI runs manually in this workspace, then re-run.",
        _ =>
            "Inspect the failure details above, resolve the cause, then re-run. Use --retry-failed to re-attempt the failed task.",
    };

    /// <summary>Full multi-line report for console/log output.</summary>
    public string Describe()
    {
        var sb = new StringBuilder();
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"PIPELINE HALTED on task (line {Result.Task.LineNumber}): {Result.Task.DisplayText}");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Failure: {Result.Failure} — {Result.ErrorMessage ?? "unknown"}");
        sb.AppendLine();

        if (History.Count > 0)
        {
            sb.AppendLine("Retry history:");
            foreach (var attempt in History)
            {
                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Attempt {attempt.Attempt}: {(attempt.Success ? "success" : $"{attempt.Failure}")} — {attempt.ErrorMessage ?? "-"}");
            }
            sb.AppendLine();
        }

        if (Result.Verification is { } verification)
        {
            sb.AppendLine(verification.Describe());
            sb.AppendLine();
        }

        if (Result.Build is { } build && build.FirstFailure is { } stage)
        {
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Build output ({stage.Name}, exit {stage.ExitCode}):");
            sb.AppendLine(stage.Output);
            sb.AppendLine();
        }

        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Suggested next action: {SuggestedNextAction}");
        return sb.ToString().TrimEnd();
    }
}
