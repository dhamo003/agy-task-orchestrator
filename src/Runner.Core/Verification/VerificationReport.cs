using System.Collections.Generic;
using System.Linq;
using System.Text;
using AntigravityTaskRunner.Terminal.Workspace;

namespace AntigravityTaskRunner.Core.Verification;

/// <summary>A single verification check with its outcome.</summary>
public sealed record VerificationCheck(string Name, bool Passed, string Detail);

/// <summary>
/// The complete result of completion verification for one attempt: every check that
/// ran, whether it passed, and the categorized workspace change set.
/// </summary>
public sealed record VerificationReport(
    bool Passed,
    IReadOnlyList<VerificationCheck> Checks,
    WorkspaceChangeSet ChangeSet)
{
    public IEnumerable<VerificationCheck> FailedChecks => Checks.Where(c => !c.Passed);

    /// <summary>Multi-line human-readable summary used in logs, retry prompts, and failure reports.</summary>
    public string Describe()
    {
        var sb = new StringBuilder();
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Verification {(Passed ? "PASSED" : "FAILED")} — {ChangeSet}");
        foreach (var check in Checks)
        {
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  [{(check.Passed ? "PASS" : "FAIL")}] {check.Name}: {check.Detail}");
        }
        return sb.ToString().TrimEnd();
    }
}
