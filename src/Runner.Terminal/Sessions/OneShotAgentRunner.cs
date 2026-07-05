using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Terminal.Detection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Runner.Logging;

namespace AntigravityTaskRunner.Terminal.Sessions;

/// <summary>
/// Runs the agent CLI in one-shot ("print") mode and waits for the real process exit —
/// the most reliable completion signal. The session still runs inside a pseudo-TTY so
/// the CLI keeps its normal output rendering. Teardown is deterministic.
/// </summary>
public sealed class OneShotAgentRunner : IAgentSessionRunner
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICompletionDetector _completionDetector;
    private readonly ITaskLogger _logger;
    private readonly RunnerOptions _options;

    public OneShotAgentRunner(
        IServiceProvider serviceProvider,
        ICompletionDetector completionDetector,
        ITaskLogger logger,
        IOptions<RunnerOptions> options)
    {
        _serviceProvider = serviceProvider;
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
        var session = _serviceProvider.GetRequiredService<ITerminalSession>();
        try
        {
            var workspacePath = ResolveWorkspacePath();
            var argv = BuildOneShotArguments(prompt, workspacePath);

            _logger.LogInfo(scope,
                $"Launching one-shot agent '{_options.Terminal.AgentCommand}' with {argv.Count} args. Waiting for process exit...");
            reportStatus?.Invoke("One-shot agent launched — waiting for process exit");
            _logger.LogInfo(scope, "[workflow] Prompt Sent");

            session.ConfigureSpawn(_options.Terminal.AgentCommand, argv, workspacePath);
            await session.StartAsync(token);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(_options.Timeout.TaskTimeout);

            TerminalSessionResult sessionResult;
            try
            {
                sessionResult = await session.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                _logger.LogWarning(scope, "One-shot agent run timed out before the process exited.");
                var (stdOut, stdErr) = session.GetCurrentOutput();
                return new AgentRunResult(stdOut + stdErr, MarkerDetected: false, MarkerSuccess: false,
                    MarkerMessage: null, ExitCode: null, TimedOut: true);
            }

            _logger.LogInfo(scope, $"[workflow] Response Received — agent process exited with code {sessionResult.ExitCode}.");

            var markerDetected = _completionDetector.DetectCompletion(
                sessionResult.StdOut, out var markerSuccess, out var markerMessage);

            return new AgentRunResult(
                sessionResult.StdOut + sessionResult.StdErr,
                MarkerDetected: markerDetected,
                MarkerSuccess: markerSuccess,
                MarkerMessage: markerMessage,
                ExitCode: sessionResult.ExitCode,
                TimedOut: false);
        }
        finally
        {
            await AgentSessionTeardown.ShutdownAsync(session, _options.Timeout.SessionTeardown, _logger, scope);
        }
    }

    /// <summary>
    /// Resolves the configured one-shot argument templates into a concrete argv list.
    /// Arguments resolving to empty are dropped so optional flags can stay unset.
    /// </summary>
    private List<string> BuildOneShotArguments(string prompt, string workspacePath)
    {
        var result = new List<string>();
        foreach (var template in _options.Terminal.OneShotArguments)
        {
            var value = template
                .Replace("{prompt}", prompt)
                .Replace("{model}", _options.Model)
                .Replace("{workspace}", workspacePath)
                .Replace("{tasksFile}", _options.TasksFile);

            if (!string.IsNullOrWhiteSpace(value))
            {
                result.Add(value);
            }
        }

        return result;
    }

    private string ResolveWorkspacePath() =>
        string.IsNullOrWhiteSpace(_options.WorkspacePath) || _options.WorkspacePath == "."
            ? Environment.CurrentDirectory
            : _options.WorkspacePath;
}
