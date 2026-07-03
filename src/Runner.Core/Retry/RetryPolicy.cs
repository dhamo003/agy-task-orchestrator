using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Core.Models;
using Runner.Logging;

namespace AntigravityTaskRunner.Core.Retry;

/// <summary>
/// Implements exponential backoff with jitter retry logic.
/// </summary>
public class RetryPolicy : IRetryPolicy
{
    private readonly RetryOptions _options;
    private readonly ITaskLogger _logger;
    private readonly Random _random;

    public RetryPolicy(IOptions<RetryOptions> options, ITaskLogger logger)
    {
        _options = options.Value;
        _logger = logger;
        _random = new Random();
    }

    public async Task<TaskExecutionResult> ExecuteAsync(Func<int, CancellationToken, Task<TaskExecutionResult>> action, CancellationToken token = default)
    {
        int attempt = 1;
        int maxAttempts = _options.MaxRetries + 1;

        while (true)
        {
            var result = await action(attempt, token);

            if (result.IsSuccess)
            {
                return result;
            }

            var logScope = new TaskLogScope($"T-{result.Task.LineNumber}", result.Task.DisplayText, attempt);

            if (attempt >= maxAttempts)
            {
                _logger.LogWarning(logScope, $"Task '{result.Task.DisplayText}' failed after {maxAttempts} attempts.");
                return result;
            }

            double delaySeconds = _options.BackoffBaseSeconds * Math.Pow(2, attempt - 1);
            delaySeconds = Math.Min(delaySeconds, _options.BackoffMaxSeconds);

            if (_options.UseJitter)
            {
                // Add +/- 20% jitter
                double jitterRange = delaySeconds * 0.2;
                double jitter = (_random.NextDouble() * 2 * jitterRange) - jitterRange;
                delaySeconds += jitter;
            }

            TimeSpan delay = TimeSpan.FromSeconds(delaySeconds);

            _logger.LogInfo(logScope, $"Task '{result.Task.DisplayText}' failed (Attempt {attempt}/{maxAttempts}). Retrying in {delay.TotalSeconds:F2}s...");

            try
            {
                await Task.Delay(delay, token);
            }
            catch (OperationCanceledException)
            {
                return result; // return the last failure if cancelled during delay
            }

            attempt++;
        }
    }
}
