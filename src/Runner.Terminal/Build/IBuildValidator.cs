namespace AntigravityTaskRunner.Terminal.Build;

/// <summary>Result of one validation stage (restore / build / test / custom).</summary>
public sealed record BuildStageResult(
    string Name,
    string CommandLine,
    bool Success,
    bool Skipped,
    int ExitCode,
    string Output,
    TimeSpan Duration,
    string? SkipReason = null);

/// <summary>Aggregated result of all build/test validation stages.</summary>
public sealed record BuildValidationResult(
    bool Success,
    bool Skipped,
    IReadOnlyList<BuildStageResult> Stages,
    string Summary)
{
    public static BuildValidationResult SkippedResult(string reason) =>
        new(Success: true, Skipped: true, Stages: [], Summary: reason);

    /// <summary>The first failing stage, if any.</summary>
    public BuildStageResult? FirstFailure
    {
        get
        {
            foreach (var stage in Stages)
            {
                if (!stage.Success && !stage.Skipped) return stage;
            }
            return null;
        }
    }
}

/// <summary>
/// The pre-task state of the workspace's test suite: which tests were already failing
/// before the agent changed anything.
/// </summary>
public sealed record TestBaseline(
    bool BuildSucceeded,
    IReadOnlySet<string> FailedTests,
    string Summary)
{
    public static TestBaseline Empty { get; } =
        new(true, new HashSet<string>(StringComparer.Ordinal), "No baseline captured.");
}

/// <summary>
/// Runs the configured build &amp; test validation commands (dotnet restore/build/test
/// by default) in the workspace. A failed build or test means the task is incomplete —
/// except that, when a <see cref="TestFailureBaseline"/> is set and
/// FailOnlyOnNewTestFailures is enabled, test failures already present BEFORE the task
/// started do not block (only regressions do).
/// </summary>
public interface IBuildValidator
{
    /// <summary>
    /// Failing-test names captured before any agent work. When set, the test stage
    /// only fails on failures NOT in this set.
    /// </summary>
    IReadOnlySet<string>? TestFailureBaseline { get; set; }

    /// <summary>
    /// Runs the validation stages once, without judging, to record which tests are
    /// already failing. Intended to be called once per run, before the first task.
    /// </summary>
    Task<TestBaseline> CaptureTestBaselineAsync(CancellationToken token = default);

    Task<BuildValidationResult> ValidateAsync(CancellationToken token = default);
}
