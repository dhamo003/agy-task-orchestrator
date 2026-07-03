namespace AntigravityTaskRunner.Configuration;

/// <summary>
/// Configuration for parallel task execution.
/// Binds to the "Runner:Parallel" section in appsettings.json.
/// </summary>
public sealed class ParallelExecutionOptions
{
    /// <summary>
    /// Maximum number of concurrent workers when running in Parallel mode.
    /// Ignored when Mode is Sequential.
    /// </summary>
    public int MaxWorkers { get; set; } = 1;

    /// <summary>
    /// Execution strategy: Sequential (one-by-one) or Parallel (concurrent).
    /// </summary>
    public ExecutionMode Mode { get; set; } = ExecutionMode.Sequential;
}
