namespace AntigravityTaskRunner.Configuration;

/// <summary>
/// Configuration for the terminal session.
/// </summary>
public sealed record TerminalOptions
{
    /// <summary>
    /// Path to the shell executable (e.g., cmd.exe).
    /// Defaults to cmd.exe on Windows.
    /// </summary>
    public string ShellPath { get; init; } = "cmd.exe";

    /// <summary>
    /// Arguments to pass to the shell on startup.
    /// Defaults to cmd /K standard argument.
    /// </summary>
    public string Arguments { get; init; } = "/K";

    /// <summary>
    /// Environment variables to pass to the shell process.
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; init; } = new();
}
