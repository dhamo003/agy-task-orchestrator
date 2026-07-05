using Runner.Logging;

namespace AntigravityTaskRunner.Terminal.Sessions;

/// <summary>
/// The raw outcome of one agent session run, before workspace/build verification.
/// </summary>
/// <param name="Output">Captured session output.</param>
/// <param name="MarkerDetected">True when a TASK_COMPLETED/TASK_FAILED marker was seen.</param>
/// <param name="MarkerSuccess">True when the detected marker was the success marker.</param>
/// <param name="MarkerMessage">The failure marker line, when present.</param>
/// <param name="ExitCode">Real process exit code (one-shot mode only).</param>
/// <param name="TimedOut">True when the run hit the task timeout.</param>
/// <param name="FailureDetail">Human-readable detail for session-level failures (e.g. CLI never became ready).</param>
public sealed record AgentRunResult(
    string Output,
    bool MarkerDetected,
    bool MarkerSuccess,
    string? MarkerMessage,
    int? ExitCode,
    bool TimedOut,
    string? FailureDetail = null)
{
    /// <summary>True when the session itself ran to a usable end (markers/exit observed, no timeout).</summary>
    public bool SessionUsable => FailureDetail is null && !TimedOut;
}

/// <summary>
/// Runs one complete, isolated agent session for a single task attempt: spawn a fresh
/// session, deliver the prompt, wait for completion, and deterministically tear the
/// session down before returning. At most one session is ever alive at a time because
/// callers await this method before doing anything else.
/// </summary>
public interface IAgentSessionRunner
{
    Task<AgentRunResult> RunAsync(
        TaskLogScope scope,
        string prompt,
        Action<string>? reportStatus = null,
        CancellationToken token = default);
}
