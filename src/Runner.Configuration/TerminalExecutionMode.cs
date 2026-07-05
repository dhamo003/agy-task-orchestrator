namespace AntigravityTaskRunner.Configuration;

/// <summary>
/// Strategy for how the Antigravity CLI is driven for each task.
/// </summary>
public enum TerminalExecutionMode
{
    /// <summary>
    /// Launch an interactive <c>agy</c> session, send the task prompt over stdin, and infer
    /// completion from the terminal output. This is the original behaviour and the default.
    /// </summary>
    Interactive = 0,

    /// <summary>
    /// Launch <c>agy</c> in one-shot ("print") mode (e.g. <c>agy -p "&lt;prompt&gt;"</c>) so the
    /// process runs the single task and then exits on its own. Completion is determined directly
    /// from the real process exit — the most reliable signal — rather than from output heuristics.
    /// </summary>
    OneShot = 1
}
