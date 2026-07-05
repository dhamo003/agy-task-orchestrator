using System;
using System.Threading;
using System.Threading.Tasks;
using AntigravityTaskRunner.Core.Models;

namespace AntigravityTaskRunner.Core.Retry;

/// <summary>
/// Defines a policy for retrying failed task attempts with failure context, and for
/// pausing (without consuming a retry) when a capacity limit is detected.
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// Executes <paramref name="attempt"/> until it succeeds, retries are exhausted,
    /// or the pause budget for capacity limits is exceeded.
    /// </summary>
    /// <param name="attempt">
    /// The attempt callback. Receives a <see cref="RetryContext"/> describing the
    /// previous failure (empty on the first attempt).
    /// </param>
    /// <param name="onAttemptCompleted">
    /// Optional observer invoked after every attempt (for checkpointing/reporting).
    /// Receives the attempt result and the context the attempt ran with.
    /// </param>
    /// <param name="initialContext">
    /// Optional starting context, used by crash recovery to resume at the checkpointed
    /// attempt number instead of attempt 1.
    /// </param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>The final result after all retries/pauses.</returns>
    Task<TaskExecutionResult> ExecuteAsync(
        Func<RetryContext, CancellationToken, Task<TaskExecutionResult>> attempt,
        Func<TaskExecutionResult, RetryContext, Task>? onAttemptCompleted = null,
        RetryContext? initialContext = null,
        CancellationToken token = default);
}
