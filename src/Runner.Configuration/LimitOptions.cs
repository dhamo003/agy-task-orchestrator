namespace AntigravityTaskRunner.Configuration;

/// <summary>
/// Configuration for detecting AI capacity limits (token limits, rate limits, quota
/// exhaustion, context overflow) and pausing/resuming execution.
/// Binds to the "Runner:Limits" section in appsettings.json.
/// </summary>
public sealed class LimitOptions
{
    /// <summary>
    /// Case-insensitive substrings/patterns in agent output that indicate a capacity limit.
    /// When any matches, the attempt is treated as PAUSED (not failed): state is
    /// checkpointed and the same task resumes after <see cref="PauseSeconds"/>.
    /// </summary>
    public List<string> LimitPatterns { get; set; } =
    [
        "rate limit exceeded", "rate limit reached", "rate-limited", "too many requests",
        "quota exceeded", "quota exhausted", "out of quota", "resource_exhausted",
        "resource has been exhausted", "token limit exceeded", "tokens exhausted",
        "maximum context length", "context length exceeded", "context overflow",
        "context window exceeded", "model is overloaded", "model is currently unavailable",
        "usage limit reached", "daily limit reached", "status code 429", "http 429"
    ];

    /// <summary>Seconds to wait before resuming the same task after a limit is detected.</summary>
    public int PauseSeconds { get; set; } = 300;

    /// <summary>
    /// Maximum consecutive limit-pauses for a single task before the pipeline halts
    /// (prevents an infinite pause loop when capacity never returns).
    /// </summary>
    public int MaxPausesPerTask { get; set; } = 12;
}
