using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Core.Models;
using Runner.Logging;

namespace AntigravityTaskRunner.Core.Retry;

/// <summary>
/// Retry policy with exponential backoff, failure-context propagation, and capacity-limit
/// pauses. Capacity limits (token/rate/quota/context) never consume a retry: execution
/// pauses for the configured interval and the SAME attempt context is retried, up to
/// <see cref="LimitOptions.MaxPausesPerTask"/> consecutive pauses.
/// </summary>
public class RetryPolicy : IRetryPolicy
{
    private readonly RetryOptions _options;
    private readonly LimitOptions _limits;
    private readonly ITaskLogger _logger;
    private readonly Random _random;

    public RetryPolicy(IOptions<RunnerOptions> options, ITaskLogger logger)
    {
        _options = options.Value.Retry;
        _limits = options.Value.Limits;
        _logger = logger;
        _random = new Random();
    }

    public async Task<TaskExecutionResult> ExecuteAsync(
        Func<RetryContext, CancellationToken, Task<TaskExecutionResult>> attempt,
        Func<TaskExecutionResult, RetryContext, Task>? onAttemptCompleted = null,
        RetryContext? initialContext = null,
        CancellationToken token = default)
    {
        var context = initialContext ?? RetryContext.Initial;
        int maxAttempts = _options.MaxRetries + 1;
        int consecutivePauses = 0;

        while (true)
        {
            var result = await attempt(context, token);

            if (onAttemptCompleted is not null)
            {
                await onAttemptCompleted(result, context);
            }

            if (result.IsSuccess)
            {
                return result;
            }

            var logScope = new TaskLogScope($"T-{result.Task.LineNumber}", result.Task.DisplayText, context.Attempt);

            // Capacity limits pause execution; they never consume a retry and never
            // advance to another task.
            if (result.IsCapacityLimit)
            {
                consecutivePauses++;
                if (consecutivePauses > _limits.MaxPausesPerTask)
                {
                    _logger.LogError(logScope,
                        $"Capacity limit persisted through {consecutivePauses - 1} pause(s); giving up on waiting.");
                    return result;
                }

                var pause = TimeSpan.FromSeconds(_limits.PauseSeconds);
                _logger.LogWarning(logScope,
                    $"[workflow] Pause — capacity limit detected ({result.Limit?.MatchedPattern}). " +
                    $"Waiting {pause.TotalMinutes:F1} min before resuming the SAME task " +
                    $"(pause {consecutivePauses}/{_limits.MaxPausesPerTask}).");

                try
                {
                    await Task.Delay(pause, token);
                }
                catch (OperationCanceledException)
                {
                    return result;
                }

                _logger.LogInfo(logScope, "[workflow] Resume — retrying the same task after capacity pause.");
                continue; // same context: not a retry, the attempt number does not advance
            }

            consecutivePauses = 0;

            if (context.Attempt >= maxAttempts)
            {
                _logger.LogWarning(logScope,
                    $"Task '{result.Task.DisplayText}' failed after {maxAttempts} attempt(s). Retries exhausted.");
                return result;
            }

            var delay = ComputeDelay(context.Attempt);
            _logger.LogInfo(logScope,
                $"[workflow] Retry — attempt {context.Attempt}/{maxAttempts} failed ({result.Failure}: {result.ErrorMessage}). " +
                $"Retrying with failure context in {delay.TotalSeconds:F2}s...");

            try
            {
                await Task.Delay(delay, token);
            }
            catch (OperationCanceledException)
            {
                return result; // return the last failure if cancelled during backoff
            }

            context = context.NextAttempt(result);
        }
    }

    private TimeSpan ComputeDelay(int attempt)
    {
        double delaySeconds = _options.BackoffBaseSeconds * Math.Pow(2, attempt - 1);
        delaySeconds = Math.Min(delaySeconds, _options.BackoffMaxSeconds);

        if (_options.UseJitter)
        {
            double jitterRange = delaySeconds * 0.2;
            double jitter = (_random.NextDouble() * 2 * jitterRange) - jitterRange;
            delaySeconds += jitter;
        }

        return TimeSpan.FromSeconds(delaySeconds);
    }
}
