using System.Collections.Generic;
using System.Linq;
using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Terminal.Sessions;
using AntigravityTaskRunner.Terminal.Workspace;
using Microsoft.Extensions.Options;

namespace AntigravityTaskRunner.Core.Verification;

/// <summary>
/// Decides whether an agent attempt really completed its task. A task is complete only
/// when every enabled check passes; "the agent said done" is never sufficient on its own.
/// </summary>
public interface ICompletionVerifier
{
    VerificationReport Verify(AgentRunResult runResult, WorkspaceChangeSet changeSet);
}

/// <summary>
/// Default implementation. Checks, in order:
/// 1. completion marker observed (and not a failure marker / non-zero exit),
/// 2. workspace changed at all,
/// 3. at least one meaningful implementation change (rejects markdown/comment/
///    whitespace/cache/tasks-file-only diffs).
/// Build/test validation is layered on top by the pipeline (it is IO-heavy and only
/// runs when this cheaper verification already passed).
/// </summary>
public sealed class CompletionVerifier : ICompletionVerifier
{
    private readonly VerificationOptions _options;

    public CompletionVerifier(IOptions<RunnerOptions> options)
    {
        _options = options.Value.Verification;
    }

    public VerificationReport Verify(AgentRunResult runResult, WorkspaceChangeSet changeSet)
    {
        var checks = new List<VerificationCheck>();

        // 1. Explicit failure signals take precedence.
        if (runResult.MarkerDetected && !runResult.MarkerSuccess)
        {
            checks.Add(new VerificationCheck("AgentOutcome", false,
                $"Agent reported failure: {runResult.MarkerMessage ?? "TASK_FAILED"}"));
        }
        else if (runResult.ExitCode is int exit && exit != 0)
        {
            checks.Add(new VerificationCheck("AgentOutcome", false,
                $"Agent process exited with non-zero code {exit}."));
        }
        else if (_options.RequireCompletionMarker && !(runResult.MarkerDetected && runResult.MarkerSuccess))
        {
            checks.Add(new VerificationCheck("CompletionMarker", false,
                "TASK_COMPLETED marker was not observed in the agent output."));
        }
        else
        {
            checks.Add(new VerificationCheck("CompletionMarker", true, "Success signal observed."));
        }

        // 2. Any workspace change at all?
        if (!changeSet.HasAnyChanges)
        {
            checks.Add(new VerificationCheck("SourceFilesModified", false,
                "No workspace files were created, modified, deleted, or renamed."));
        }
        else
        {
            checks.Add(new VerificationCheck("SourceFilesModified", true,
                $"{changeSet.Changes.Count} file change(s) detected."));
        }

        // 3. Meaningful implementation diff?
        if (_options.RequireMeaningfulDiff)
        {
            if (changeSet.HasMeaningfulChanges)
            {
                checks.Add(new VerificationCheck("MeaningfulImplementationDiff", true,
                    $"{changeSet.MeaningfulChanges.Count} meaningful change(s): " +
                    string.Join(", ", changeSet.MeaningfulChanges.Take(10).Select(c => $"{c.Kind}:{c.Path}"))));
            }
            else if (changeSet.HasAnyChanges)
            {
                checks.Add(new VerificationCheck("MeaningfulImplementationDiff", false,
                    "Only non-implementation changes detected (documentation, comments/whitespace-only, " +
                    "cache, or tasks-file edits): " +
                    string.Join(", ", changeSet.Changes.Take(10).Select(c => $"{c.Kind}:{c.Path}"))));
            }
            else
            {
                checks.Add(new VerificationCheck("MeaningfulImplementationDiff", false,
                    "No changes to evaluate."));
            }
        }

        return new VerificationReport(checks.All(c => c.Passed), checks, changeSet);
    }
}
