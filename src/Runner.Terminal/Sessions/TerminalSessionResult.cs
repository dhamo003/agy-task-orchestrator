namespace AntigravityTaskRunner.Terminal.Sessions;

/// <summary>
/// Represents the result of a completed terminal session.
/// </summary>
/// <param name="ExitCode">The exit code of the process.</param>
/// <param name="StdOut">The standard output captured from the process.</param>
/// <param name="StdErr">The standard error captured from the process.</param>
/// <param name="Duration">The duration for which the session ran.</param>
public sealed record TerminalSessionResult(
    int ExitCode,
    string StdOut,
    string StdErr,
    TimeSpan Duration
);
