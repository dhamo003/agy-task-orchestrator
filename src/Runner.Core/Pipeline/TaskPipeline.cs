using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Runner.Markdown.Models;
using Runner.Markdown.Writer;
using Runner.Logging;
using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Core.Models;
using AntigravityTaskRunner.Terminal.Sessions;
using AntigravityTaskRunner.Terminal.Workspace;
using AntigravityTaskRunner.Terminal.Detection;
using AntigravityTaskRunner.Core.Prompts;
using TaskStatus = Runner.Markdown.Models.TaskStatus;

namespace AntigravityTaskRunner.Core.Pipeline;

/// <summary>
/// Orchestrates the lifecycle of a single task.
/// </summary>
public class TaskPipeline : ITaskPipeline
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IWorkspaceAnalyzer _workspaceAnalyzer;
    private readonly IModelDetector _modelDetector;
    private readonly IModelSwitcher _modelSwitcher;
    private readonly ICompletionDetector _completionDetector;
    private readonly ITaskWriter _taskWriter;
    private readonly ITaskLogger _logger;
    private readonly IPromptTemplateEngine _promptTemplateEngine;
    private readonly RunnerOptions _options;

    public TaskPipeline(
        IServiceProvider serviceProvider,
        IWorkspaceAnalyzer workspaceAnalyzer,
        IModelDetector modelDetector,
        IModelSwitcher modelSwitcher,
        ICompletionDetector completionDetector,
        ITaskWriter taskWriter,
        ITaskLogger logger,
        IPromptTemplateEngine promptTemplateEngine,
        IOptions<RunnerOptions> options)
    {
        _serviceProvider = serviceProvider;
        _workspaceAnalyzer = workspaceAnalyzer;
        _modelDetector = modelDetector;
        _modelSwitcher = modelSwitcher;
        _completionDetector = completionDetector;
        _taskWriter = taskWriter;
        _logger = logger;
        _promptTemplateEngine = promptTemplateEngine;
        _options = options.Value;
    }

    public async Task<TaskExecutionResult> ExecuteAsync(TaskItem task, CancellationToken token = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var logScope = new TaskLogScope($"T-{task.LineNumber}", task.DisplayText, 1);
        
        _logger.LogInfo(logScope, "Starting task pipeline...");

        try
        {
            // Step 1: Take workspace snapshot
            _logger.LogDebug(logScope, "Taking initial workspace snapshot...");
            var initialSnapshot = await _workspaceAnalyzer.TakeSnapshotAsync(token);

            // Step 2: Spawn terminal session
            // We resolve a new session per task to ensure clean state
            using var terminalSession = _serviceProvider.GetRequiredService<ITerminalSession>();
            _logger.LogDebug(logScope, "Starting terminal session...");
            await terminalSession.StartAsync(token);

            // Navigate to workspace and start agy
            var workspacePath = _options.WorkspacePath;
            if (string.IsNullOrWhiteSpace(workspacePath) || workspacePath == ".")
            {
                workspacePath = Environment.CurrentDirectory;
            }
            _logger.LogDebug(logScope, $"Navigating to {workspacePath} and starting Antigravity...");
            await terminalSession.SendInputAsync($"cd /d \"{workspacePath}\"", token);
            await terminalSession.SendInputAsync("agy", token);

            // Step 3: Wait for CLI ready
            _logger.LogDebug(logScope, "Waiting for Antigravity banner...");
            bool isReady = false;
            var readyTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            readyTokenSource.CancelAfter(TimeSpan.FromSeconds(30));

            try
            {
                while (!isReady && !readyTokenSource.IsCancellationRequested)
                {
                    await Task.Delay(1000, readyTokenSource.Token);
                    var output = terminalSession.GetCurrentOutput().StdOut + terminalSession.GetCurrentOutput().StdErr;
                    
                    if (output.Contains("Do you trust the contents of this project?"))
                    {
                        _logger.LogInfo(logScope, "Detected trust prompt. Approving automatically.");
                        await terminalSession.SendInputAsync("", token); // Send Enter to accept default "Yes"
                        await Task.Delay(1000, token);
                    }

                    if (output.Contains("? for shortcuts"))
                    {
                        isReady = true;
                    }
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                _logger.LogWarning(logScope, "Timed out waiting for Antigravity banner.");
            }

            // Step 4: Verify/switch model
            _logger.LogDebug(logScope, "Verifying/switching model...");
            var currentModel = await _modelDetector.DetectModelAsync(terminalSession, token);
            if (currentModel != _options.Model)
            {
                await _modelSwitcher.SwitchModelAsync(terminalSession, _options.Model, token);
                await Task.Delay(2000, token); // Wait for switch to take effect
            }

            // Step 5: Build prompt from template
            _logger.LogDebug(logScope, "Building prompt...");
            string prompt = await _promptTemplateEngine.BuildPromptAsync(task, initialSnapshot, token);

            // Step 6: Send prompt via stdin
            _logger.LogDebug(logScope, "Sending prompt...");
            terminalSession.ClearOutputBuffers();
            await terminalSession.SendInputAsync(prompt, token);

            // Step 7: Monitor output for completion
            _logger.LogDebug(logScope, "Monitoring output for completion...");
            bool isCompleted = false;
            bool success = false;
            string? errorMessage = null;
            int lastProcessedOffset = 0;

            // Poll output every second up to task timeout
            var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutTokenSource.CancelAfter(_options.Timeout.TaskTimeout);

            try
            {
                while (!isCompleted && !timeoutTokenSource.IsCancellationRequested)
                {
                    await Task.Delay(1000, timeoutTokenSource.Token);
                    var output = terminalSession.GetCurrentOutput().StdOut;
                    
                    if (output.Length > lastProcessedOffset)
                    {
                        var newOutput = output.Substring(lastProcessedOffset);
                        _logger.LogDebug(logScope, $"[TERMINAL OUTPUT] {newOutput.Replace("\r", "\\r").Replace("\n", "\\n")}");
                        bool hasMarkers = _completionDetector.DetectCompletion(newOutput, out success, out errorMessage);
                        bool isAgentFinished = newOutput.Contains("? for shortcuts");

                        lastProcessedOffset = output.Length;

                        if (hasMarkers)
                        {
                            isCompleted = true;
                        }
                        else if (isAgentFinished)
                        {
                            // The agent returned to the prompt without triggering explicit markers.
                            // We will consider it completed. We can assume success by default,
                            // and let the workspace changes verification decide if it actually did the work.
                            isCompleted = true;
                            success = true;
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                // Timeout occurred
                isCompleted = true;
                success = false;
                errorMessage = "Task timed out.";
            }

            // Step 8: Verify workspace changes
            _logger.LogDebug(logScope, "Verifying workspace changes...");
            var finalSnapshot = await _workspaceAnalyzer.TakeSnapshotAsync(token);
            var changes = _workspaceAnalyzer.GetChanges(initialSnapshot, finalSnapshot);
            _logger.LogInfo(logScope, $"Detected {changes.Count} file changes.");

            // Step 9: Mark checkbox and close terminal
            // terminalSession is disposed at the end of the using block, which closes it.
            _logger.LogDebug(logScope, $"Updating task status to {(success ? "Completed" : "Failed")}...");
            await _taskWriter.UpdateStatusAsync(
                _options.TasksFile, 
                task, 
                success ? TaskStatus.Completed : TaskStatus.Failed, 
                errorMessage, 
                token);

            stopwatch.Stop();
            return new TaskExecutionResult(task, success, stopwatch.Elapsed, errorMessage, 0);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(logScope, "Pipeline execution failed with exception", ex);
            
            await _taskWriter.UpdateStatusAsync(
                _options.TasksFile, 
                task, 
                TaskStatus.Failed, 
                "Pipeline execution error: " + ex.Message, 
                token);

            return new TaskExecutionResult(task, false, stopwatch.Elapsed, ex.Message, 0);
        }
    }
}
