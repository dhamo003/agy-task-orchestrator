namespace AntigravityTaskRunner.Terminal.Sessions;

/// <summary>
/// Represents a session interacting with a terminal/shell process.
/// </summary>
public interface ITerminalSession : IDisposable
{
    /// <summary>
    /// Starts the terminal session.
    /// </summary>
    /// <param name="token">Cancellation token to cancel startup.</param>
    /// <returns>A task that completes when the process starts.</returns>
    Task StartAsync(CancellationToken token = default);

    /// <summary>
    /// Sends input to the standard input stream of the terminal.
    /// </summary>
    /// <param name="input">The input string to send.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>A task that completes when the input is written.</returns>
    Task SendInputAsync(string input, CancellationToken token = default);

    /// <summary>
    /// Reads output from the standard output and standard error streams.
    /// Note: Implementations usually buffer the output as it arrives, and this could return the current buffer.
    /// Or maybe this is used if we want to stream output. The requirements are async stdout/stderr streaming capture.
    /// For detecting completion, we might need to get the recent output.
    /// Let's define it as getting the currently accumulated stdout/stderr.
    /// </summary>
    /// <returns>The accumulated standard output and standard error.</returns>
    (string StdOut, string StdErr) GetCurrentOutput();

    /// <summary>
    /// Clears the currently accumulated output buffers.
    /// </summary>
    void ClearOutputBuffers();

    /// <summary>
    /// Waits for the terminal session to exit.
    /// </summary>
    /// <param name="token">Cancellation token to abort waiting.</param>
    /// <returns>The result of the terminal session.</returns>
    Task<TerminalSessionResult> WaitForExitAsync(CancellationToken token = default);

    /// <summary>
    /// Forcefully kills the terminal session if it is running.
    /// </summary>
    /// <param name="token">Cancellation token.</param>
    /// <returns>A task that completes when the process is killed.</returns>
    Task KillAsync(CancellationToken token = default);
}
