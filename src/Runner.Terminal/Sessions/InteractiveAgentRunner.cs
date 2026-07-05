using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Terminal.Detection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Runner.Logging;

namespace AntigravityTaskRunner.Terminal.Sessions;

/// <summary>
/// Drives one interactive agent session inside a pseudo-terminal: starts the shell,
/// launches the agent CLI, waits for readiness, verifies/switches the model, sends the
/// prompt, then monitors output for explicit completion markers (authoritative) or the
/// idle-footer heuristic. The session's process tree is always torn down before this
/// method returns.
/// </summary>
public sealed class InteractiveAgentRunner : IAgentSessionRunner
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Marker detection re-scans this many characters BEFORE the last processed offset.
    /// Output arrives in arbitrary 1-second chunks, so a marker line can be split across
    /// two polls ("…TASK_COMPL" | "ETED…"); the overlap guarantees every line is
    /// eventually seen whole. Re-scanning is idempotent, so the overlap is harmless.
    /// </summary>
    private const int ScanOverlapChars = 8192;

    private readonly IServiceProvider _serviceProvider;
    private readonly IModelDetector _modelDetector;
    private readonly IModelSwitcher _modelSwitcher;
    private readonly ICompletionDetector _completionDetector;
    private readonly ITaskLogger _logger;
    private readonly RunnerOptions _options;

    public InteractiveAgentRunner(
        IServiceProvider serviceProvider,
        IModelDetector modelDetector,
        IModelSwitcher modelSwitcher,
        ICompletionDetector completionDetector,
        ITaskLogger logger,
        IOptions<RunnerOptions> options)
    {
        _serviceProvider = serviceProvider;
        _modelDetector = modelDetector;
        _modelSwitcher = modelSwitcher;
        _completionDetector = completionDetector;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<AgentRunResult> RunAsync(
        TaskLogScope scope,
        string prompt,
        Action<string>? reportStatus = null,
        CancellationToken token = default)
    {
        // A fresh session per attempt guarantees clean context.
        var session = _serviceProvider.GetRequiredService<ITerminalSession>();
        try
        {
            reportStatus?.Invoke("Starting terminal session");
            await session.StartAsync(token);

            var workspacePath = ResolveWorkspacePath();
            _logger.LogDebug(scope, $"Navigating to {workspacePath} and starting agent CLI...");
            await session.SendInputAsync($"cd /d \"{workspacePath}\"", token);
            await session.SendInputAsync($"{_options.Terminal.AgentCommand} --dangerously-skip-permissions", token);

            reportStatus?.Invoke("Waiting for agent CLI to become ready");
            if (!await WaitForReadyAsync(session, scope, token))
            {
                return new AgentRunResult(
                    CurrentOutput(session), MarkerDetected: false, MarkerSuccess: false, MarkerMessage: null,
                    ExitCode: null, TimedOut: false,
                    FailureDetail: "Timed out waiting for the agent CLI to become ready.");
            }

            reportStatus?.Invoke("Verifying model");
            await EnsureModelAsync(session, scope, token);
            session.ClearOutputBuffers();

            reportStatus?.Invoke("Prompt sent — AI processing");
            _logger.LogInfo(scope, "[workflow] Prompt Sent");
            await session.SendInputAsync(prompt, token);

            return await MonitorAsync(session, scope, reportStatus, token);
        }
        finally
        {
            await AgentSessionTeardown.ShutdownAsync(session, _options.Timeout.SessionTeardown, _logger, scope);
        }
    }

    private async Task<bool> WaitForReadyAsync(ITerminalSession session, TaskLogScope scope, CancellationToken token)
    {
        using var readyCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        readyCts.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            while (!readyCts.IsCancellationRequested)
            {
                await Task.Delay(1000, readyCts.Token);
                var output = CurrentOutput(session);

                if (output.Contains("Do you trust the contents of this project?"))
                {
                    _logger.LogInfo(scope, "Detected trust prompt. Approving automatically.");
                    await session.SendInputAsync("", token); // Enter accepts default "Yes"
                    await Task.Delay(1000, token);
                }

                if (output.Contains("? for shortcuts"))
                {
                    return true;
                }
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            _logger.LogWarning(scope, "Timed out waiting for the agent CLI banner.");
        }

        return false;
    }

    private async Task EnsureModelAsync(ITerminalSession session, TaskLogScope scope, CancellationToken token)
    {
        _logger.LogDebug(scope, "Verifying/switching model...");
        var currentModel = await _modelDetector.DetectModelAsync(session, token);
        var matches = currentModel != null &&
                      NormalizeModelName(currentModel) == NormalizeModelName(_options.Model);
        if (!matches)
        {
            await _modelSwitcher.SwitchModelAsync(session, _options.Model, token);
            await Task.Delay(2000, token); // Wait for the switch to take effect
        }
    }

    private async Task<AgentRunResult> MonitorAsync(
        ITerminalSession session,
        TaskLogScope scope,
        Action<string>? reportStatus,
        CancellationToken token)
    {
        var promptSentAt = DateTime.UtcNow;
        var lastOutputChangeTime = DateTime.UtcNow;
        var lastHeartbeat = DateTime.UtcNow;
        int lastOutputLength = 0;
        int lastProcessedOffset = 0;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutCts.CancelAfter(_options.Timeout.TaskTimeout);

        try
        {
            while (true)
            {
                await Task.Delay(1000, timeoutCts.Token);
                var output = session.GetCurrentOutput().StdOut;

                if (output.Length != lastOutputLength)
                {
                    lastOutputChangeTime = DateTime.UtcNow;
                    lastOutputLength = output.Length;
                }

                if (output.Length > lastProcessedOffset)
                {
                    var newOutput = output[lastProcessedOffset..];
                    _logger.LogDebug(scope, $"[TERMINAL OUTPUT] {newOutput.Replace("\r", "\\r").Replace("\n", "\\n")}");

                    // Detect over an overlap window so lines split across poll chunks
                    // are still seen whole.
                    var scanStart = Math.Max(0, lastProcessedOffset - ScanOverlapChars);
                    lastProcessedOffset = output.Length;

                    if (_completionDetector.DetectCompletion(output[scanStart..], out var markerSuccess, out var markerMessage))
                    {
                        _logger.LogInfo(scope, "[workflow] Response Received (explicit marker)");
                        return new AgentRunResult(output, MarkerDetected: true, MarkerSuccess: markerSuccess,
                            MarkerMessage: markerMessage, ExitCode: null, TimedOut: false);
                    }
                }

                // Heartbeat so the UI never appears frozen during long AI processing.
                if (DateTime.UtcNow - lastHeartbeat >= HeartbeatInterval)
                {
                    lastHeartbeat = DateTime.UtcNow;
                    var elapsed = DateTime.UtcNow - promptSentAt;
                    var silent = (DateTime.UtcNow - lastOutputChangeTime).TotalSeconds;
                    reportStatus?.Invoke($"AI processing — elapsed {elapsed:hh\\:mm\\:ss}, {output.Length} chars output, silent {silent:F0}s");
                }

                // Idle-footer completion: only after the grace period, with sustained
                // silence, and with the idle footer rendered at the bottom of the screen.
                var gracePeriodElapsed = DateTime.UtcNow - promptSentAt >= _options.Timeout.MinPromptProcessing;
                var isSilent = DateTime.UtcNow - lastOutputChangeTime >= _options.Timeout.IdleSilenceTimeout;
                if (gracePeriodElapsed && isSilent)
                {
                    var tail = output.Length > 300 ? output[^300..] : output;
                    if (tail.Contains("? for shortcuts", StringComparison.OrdinalIgnoreCase))
                    {
                        // Safety net: before concluding "no marker", scan the COMPLETE
                        // accumulated output once. This catches markers that windowed
                        // scanning missed for any reason (heavy TUI repaints, very
                        // large chunks, escape-sequence interleaving…).
                        if (_completionDetector.DetectCompletion(output, out var finalSuccess, out var finalMessage))
                        {
                            _logger.LogInfo(scope, "[workflow] Response Received (explicit marker found in final full-output scan)");
                            return new AgentRunResult(output, MarkerDetected: true, MarkerSuccess: finalSuccess,
                                MarkerMessage: finalMessage, ExitCode: null, TimedOut: false);
                        }

                        _logger.LogInfo(scope, "[workflow] Response Received (agent returned to idle without explicit marker)");
                        return new AgentRunResult(output, MarkerDetected: false, MarkerSuccess: false,
                            MarkerMessage: null, ExitCode: null, TimedOut: false);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            var finalOutput = CurrentOutput(session);
            if (_completionDetector.DetectCompletion(finalOutput, out var lateSuccess, out var lateMessage))
            {
                _logger.LogInfo(scope, "[workflow] Response Received (marker found in final scan at timeout)");
                return new AgentRunResult(finalOutput, MarkerDetected: true, MarkerSuccess: lateSuccess,
                    MarkerMessage: lateMessage, ExitCode: null, TimedOut: false);
            }

            _logger.LogWarning(scope, "Task timed out while monitoring agent output.");
            return new AgentRunResult(finalOutput, MarkerDetected: false, MarkerSuccess: false,
                MarkerMessage: null, ExitCode: null, TimedOut: true);
        }
    }

    private string ResolveWorkspacePath() =>
        string.IsNullOrWhiteSpace(_options.WorkspacePath) || _options.WorkspacePath == "."
            ? Environment.CurrentDirectory
            : _options.WorkspacePath;

    private static string CurrentOutput(ITerminalSession session)
    {
        var (stdOut, stdErr) = session.GetCurrentOutput();
        return stdOut + stdErr;
    }

    private static string NormalizeModelName(string model) =>
        model.Replace("-", "").Replace(" ", "").Replace("(", "").Replace(")", "").ToLowerInvariant();
}
