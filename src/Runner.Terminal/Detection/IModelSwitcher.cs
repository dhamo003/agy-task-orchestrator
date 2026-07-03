using AntigravityTaskRunner.Terminal.Sessions;

namespace AntigravityTaskRunner.Terminal.Detection;

/// <summary>
/// Switches the AI model used in the active terminal session.
/// </summary>
public interface IModelSwitcher
{
    /// <summary>
    /// Switches the terminal session to the target model.
    /// </summary>
    /// <param name="session">The active terminal session.</param>
    /// <param name="targetModel">The target model name to switch to.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>True if switch was initiated successfully.</returns>
    Task<bool> SwitchModelAsync(ITerminalSession session, string targetModel, CancellationToken token = default);
}
