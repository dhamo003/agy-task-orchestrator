namespace AntigravityTaskRunner.Configuration;

/// <summary>
/// Configuration for retry behavior on failed task executions.
/// Binds to the "Runner:Retry" section in appsettings.json.
/// </summary>
public sealed class RetryOptions
{
    /// <summary>
    /// Maximum number of retry attempts per task before marking as failed.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Base delay in seconds for exponential backoff between retries.
    /// Actual delay = BackoffBaseSeconds * 2^(attempt - 1), capped at BackoffMaxSeconds.
    /// </summary>
    public double BackoffBaseSeconds { get; set; } = 5;

    /// <summary>
    /// Maximum delay in seconds between retry attempts (backoff cap).
    /// </summary>
    public double BackoffMaxSeconds { get; set; } = 300;

    /// <summary>
    /// When true, adds random jitter to backoff delays to prevent thundering herd.
    /// Defaults to false so unattended runs are deterministic.
    /// </summary>
    public bool UseJitter { get; set; }
}
