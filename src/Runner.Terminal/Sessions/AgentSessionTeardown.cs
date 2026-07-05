using Runner.Logging;

namespace AntigravityTaskRunner.Terminal.Sessions;

/// <summary>
/// Deterministic session teardown shared by all session runners: kills the agent
/// process tree and waits (bounded) for the OS to reap it, guaranteeing no orphaned
/// sessions survive into the next task.
/// </summary>
internal static class AgentSessionTeardown
{
    public static async Task ShutdownAsync(
        ITerminalSession session,
        TimeSpan teardownTimeout,
        ITaskLogger logger,
        TaskLogScope scope)
    {
        using var teardownCts = new CancellationTokenSource(teardownTimeout);
        try
        {
            logger.LogDebug(scope, "Tearing down terminal session (killing agent process tree)...");
            await session.KillAsync(teardownCts.Token);
            await session.WaitForExitAsync(teardownCts.Token);
            logger.LogDebug(scope, "Terminal session fully terminated.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(scope, $"Error during terminal session teardown: {ex.Message}");
        }
        finally
        {
            session.Dispose();
        }
    }
}
