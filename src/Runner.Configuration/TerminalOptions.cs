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

    /// <summary>
    /// How the Antigravity CLI is driven for each task. Defaults to
    /// <see cref="TerminalExecutionMode.Interactive"/> to preserve existing behaviour.
    /// Set to <see cref="TerminalExecutionMode.OneShot"/> to run <c>agy</c> in print mode so
    /// completion is detected from the real process exit.
    /// </summary>
    public TerminalExecutionMode ExecutionMode { get; init; } = TerminalExecutionMode.Interactive;

    /// <summary>
    /// Executable used to launch the Antigravity agent in one-shot mode. Defaults to <c>agy</c>.
    /// </summary>
    public string AgentCommand { get; init; } = "agy";

    /// <summary>
    /// Argument template used to launch the agent in one-shot mode, as an ordered argv list
    /// (each element is passed as a distinct argument, so no shell quoting is required even for
    /// long prompts). Supported placeholders, substituted per element:
    /// <c>{prompt}</c>, <c>{model}</c>, <c>{workspace}</c>, <c>{tasksFile}</c>.
    /// Any element that resolves to an empty/whitespace string is dropped, so optional flags such
    /// as the model can be omitted simply by leaving their value unset.
    /// Default: <c>agy -p "{prompt}" --approve all</c>.
    /// </summary>
    public List<string> OneShotArguments { get; init; } =
        ["-p", "{prompt}", "--approve", "all"];
}
