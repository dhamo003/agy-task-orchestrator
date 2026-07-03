namespace AntigravityTaskRunner.Configuration;

/// <summary>
/// Configuration for timeout limits across task, session, and model operations.
/// Binds to the "Runner:Timeout" section in appsettings.json.
/// </summary>
public sealed class TimeoutOptions
{
    /// <summary>
    /// Maximum time in minutes allowed for a single task execution.
    /// </summary>
    public int TaskTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Maximum time in minutes allowed for an entire terminal session.
    /// </summary>
    public int SessionTimeoutMinutes { get; set; } = 60;

    /// <summary>
    /// Maximum time in seconds allowed for a model switch operation.
    /// </summary>
    public int ModelSwitchTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets the task timeout as a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan TaskTimeout => TimeSpan.FromMinutes(TaskTimeoutMinutes);

    /// <summary>
    /// Gets the session timeout as a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan SessionTimeout => TimeSpan.FromMinutes(SessionTimeoutMinutes);

    /// <summary>
    /// Gets the model switch timeout as a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan ModelSwitchTimeout => TimeSpan.FromSeconds(ModelSwitchTimeoutSeconds);
}
