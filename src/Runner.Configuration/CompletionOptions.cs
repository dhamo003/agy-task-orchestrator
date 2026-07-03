namespace AntigravityTaskRunner.Configuration;

/// <summary>
/// Configuration for detecting task completion from CLI output streams.
/// Binds to the "Runner:Completion" section in appsettings.json.
/// </summary>
public sealed class CompletionOptions
{
    /// <summary>
    /// Markers in CLI output that indicate a task completed successfully.
    /// Any match triggers a success determination.
    /// </summary>
    public List<string> SuccessMarkers { get; set; } = ["TASK_COMPLETED"];

    /// <summary>
    /// Markers in CLI output that indicate a task has failed.
    /// Any match triggers a failure determination.
    /// </summary>
    public List<string> FailureMarkers { get; set; } = ["TASK_FAILED"];

    /// <summary>
    /// Markers in CLI output that indicate a task has timed out.
    /// Any match triggers a timeout determination.
    /// </summary>
    public List<string> TimeoutMarkers { get; set; } = ["timed out", "timeout"];

    /// <summary>
    /// When true, marker matching is case-insensitive.
    /// </summary>
    public bool CaseInsensitive { get; set; } = true;
}
