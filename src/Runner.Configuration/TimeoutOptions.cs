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
    /// Minimum number of seconds the pipeline must wait after sending the task prompt
    /// before it will accept "? for shortcuts" as an agent-finished signal.
    /// </summary>
    public int MinPromptProcessingSeconds { get; set; } = 15;

    /// <summary>
    /// Number of seconds of terminal output silence (no new output) required
    /// before we consider "? for shortcuts" to be a valid completion signal.
    /// Defaults to 15 seconds in production, but can be set to 0 in tests.
    /// </summary>
    public int IdleSilenceTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Maximum time in seconds the pipeline will spend tearing down a task's terminal session
    /// (killing the Antigravity CLI process tree and waiting for it to exit) before giving up.
    /// This is an upper bound to prevent teardown from hanging; the normal path completes as soon
    /// as the real process-exit signal is observed.
    /// </summary>
    public int SessionTeardownSeconds { get; set; } = 30;

    /// <summary>Gets the task timeout as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan TaskTimeout => TimeSpan.FromMinutes(TaskTimeoutMinutes);

    /// <summary>Gets the session timeout as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan SessionTimeout => TimeSpan.FromMinutes(SessionTimeoutMinutes);

    /// <summary>Gets the model switch timeout as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan ModelSwitchTimeout => TimeSpan.FromSeconds(ModelSwitchTimeoutSeconds);

    /// <summary>Gets the minimum prompt processing guard as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan MinPromptProcessing => TimeSpan.FromSeconds(MinPromptProcessingSeconds);

    /// <summary>Gets the idle silence timeout as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan IdleSilenceTimeout => TimeSpan.FromSeconds(IdleSilenceTimeoutSeconds);

    /// <summary>Gets the session teardown timeout as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan SessionTeardown => TimeSpan.FromSeconds(SessionTeardownSeconds);
}
