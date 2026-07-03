using System;
using System.Threading;
using System.Threading.Tasks;
using AntigravityTaskRunner.Core.Models;

namespace AntigravityTaskRunner.Core.Retry;

/// <summary>
/// Defines a policy for retrying failed task executions.
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// Executes the specified action with retry logic.
    /// </summary>
    /// <param name="action">The action to execute, which returns a TaskExecutionResult.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>The final TaskExecutionResult after all retries (if any).</returns>
    Task<TaskExecutionResult> ExecuteAsync(Func<int, CancellationToken, Task<TaskExecutionResult>> action, CancellationToken token = default);
}
