using AntigravityTaskRunner.Terminal.Sessions;

namespace AntigravityTaskRunner.Terminal.Detection;

/// <summary>
/// Detects the currently active AI model in the terminal session.
/// </summary>
public interface IModelDetector
{
    /// <summary>
    /// Detects the current model by interrogating the terminal session.
    /// </summary>
    /// <param name="session">The active terminal session.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>The name of the detected model, or null if detection failed.</returns>
    Task<string?> DetectModelAsync(ITerminalSession session, CancellationToken token = default);
}
