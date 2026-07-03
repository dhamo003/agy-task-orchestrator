namespace AntigravityTaskRunner.Terminal.Detection;

/// <summary>
/// Detects whether a task has completed (successfully or failed) based on terminal output.
/// </summary>
public interface ICompletionDetector
{
    /// <summary>
    /// Detects task completion from the given terminal output.
    /// </summary>
    /// <param name="output">The combined stdout/stderr output from the terminal.</param>
    /// <param name="success">Output parameter set to true if successful completion was detected, false if failure.</param>
    /// <param name="errorMessage">Output parameter containing the failure reason if detected.</param>
    /// <returns>True if a completion marker (success or failure) was found; otherwise, false.</returns>
    bool DetectCompletion(string output, out bool success, out string? errorMessage);
}
