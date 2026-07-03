namespace AntigravityTaskRunner.Configuration;

/// <summary>
/// Defines whether tasks are executed sequentially or in parallel.
/// </summary>
public enum ExecutionMode
{
    /// <summary>
    /// Tasks are executed one at a time, in order.
    /// </summary>
    Sequential,

    /// <summary>
    /// Tasks are executed concurrently, up to the configured worker limit.
    /// </summary>
    Parallel
}
