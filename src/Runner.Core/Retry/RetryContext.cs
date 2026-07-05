using System.Collections.Generic;
using System.Linq;
using System.Text;
using AntigravityTaskRunner.Core.Models;

namespace AntigravityTaskRunner.Core.Retry;

/// <summary>
/// Everything the next attempt needs to know about why the previous attempt failed,
/// so the AI can fix the actual problem instead of repeating the same work.
/// </summary>
public sealed record RetryContext(
    int Attempt,
    TaskExecutionResult? PreviousResult)
{
    /// <summary>First attempt: no failure context.</summary>
    public static RetryContext Initial { get; } = new(1, null);

    public bool IsRetry => Attempt > 1 && PreviousResult is not null;

    public RetryContext NextAttempt(TaskExecutionResult failed) => new(Attempt + 1, failed);

    /// <summary>
    /// Builds the corrective-guidance block injected into the retry prompt: previous
    /// failure reason, verification failures, build/test errors, and which files were
    /// (and were not) modified.
    /// </summary>
    public string BuildGuidance()
    {
        if (!IsRetry || PreviousResult is null)
        {
            return string.Empty;
        }

        var previous = PreviousResult;
        var sb = new StringBuilder();
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"IMPORTANT — this is attempt {Attempt}. The previous attempt FAILED. Do not repeat it; fix the problem described below.");
        sb.AppendLine();
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Previous failure ({previous.Failure}): {previous.ErrorMessage ?? "unknown"}");

        if (previous.Verification is { } verification)
        {
            foreach (var check in verification.FailedChecks)
            {
                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"- Verification failed: {check.Name} — {check.Detail}");
            }

            var modified = verification.ChangeSet.Changes.Select(c => $"{c.Kind}:{c.Path}").ToList();
            sb.AppendLine(modified.Count > 0
                ? $"- Files changed last attempt: {string.Join(", ", modified.Take(20))}"
                : "- No files were changed last attempt. You MUST modify the actual project source files.");

            if (verification.ChangeSet.HasAnyChanges && !verification.ChangeSet.HasMeaningfulChanges)
            {
                sb.AppendLine("- Those changes were NOT real implementation changes (documentation, comments, whitespace, or cache only). Write or modify actual code this time.");
            }
        }

        if (previous.Build is { } build && !build.Success)
        {
            var failure = build.FirstFailure;
            if (failure is not null)
            {
                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"- Build/test stage '{failure.Name}' failed (exit {failure.ExitCode}). Fix these errors:");
                sb.AppendLine(Tail(failure.Output, 4000));
            }
        }

        sb.AppendLine();
        sb.AppendLine(GuidanceFor(previous.Failure));
        return sb.ToString().TrimEnd();
    }

    private static string GuidanceFor(FailureKind kind) => kind switch
    {
        FailureKind.MarkerMissing =>
            "Guidance: complete the task fully, then print a final line containing exactly \"TASK_COMPLETED\" (without the quotes) — alone on its own line, with no other words or punctuation.",
        FailureKind.NoChanges =>
            "Guidance: the task requires editing real project files. Locate the relevant source files and implement the change before printing TASK_COMPLETED.",
        FailureKind.NoMeaningfulChanges =>
            "Guidance: modify actual implementation code (not markdown, comments, or formatting). The verifier ignores non-code edits.",
        FailureKind.BuildFailed =>
            "Guidance: run the build mentally against your changes; fix every compiler error listed above before finishing.",
        FailureKind.TestsFailed =>
            "Guidance: your changes introduced NEW failing tests (regressions). Read the test output above to see exactly which assertions failed, and fix your changes so those tests pass again.",
        FailureKind.Timeout =>
            "Guidance: the previous attempt ran out of time. Work incrementally: make the minimal correct change for THIS task only, verify, and print TASK_COMPLETED.",
        FailureKind.AgentReportedFailure =>
            "Guidance: the previous attempt gave up. Re-read the task, break it into smaller steps, and complete each one.",
        FailureKind.SessionFailure =>
            "Guidance: the previous session failed to start correctly; simply complete the task as specified.",
        _ =>
            "Guidance: analyse the failure above and take a different approach this time.",
    };

    private static string Tail(string value, int maxChars) =>
        value.Length <= maxChars ? value : "…" + value[^maxChars..];
}
