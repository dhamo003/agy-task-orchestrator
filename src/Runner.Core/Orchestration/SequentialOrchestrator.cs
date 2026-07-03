using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Core.Models;
using AntigravityTaskRunner.Core.Pipeline;
using AntigravityTaskRunner.Core.Progress;
using AntigravityTaskRunner.Core.Retry;
using Runner.Markdown.Parser;
using Runner.Markdown.Writer;
using Runner.Markdown.Models;
using Runner.Logging;
using TaskStatus = Runner.Markdown.Models.TaskStatus;

namespace AntigravityTaskRunner.Core.Orchestration;

/// <summary>
/// Orchestrator that runs tasks sequentially.
/// </summary>
public class SequentialOrchestrator : ITaskOrchestrator
{
    private readonly ITaskParser _taskParser;
    private readonly ITaskWriter _taskWriter;
    private readonly ITaskPipeline _taskPipeline;
    private readonly IRetryPolicy _retryPolicy;
    private readonly IProgressTracker _progressTracker;
    private readonly ITaskLogger _logger;
    private readonly RunnerOptions _options;

    public SequentialOrchestrator(
        ITaskParser taskParser,
        ITaskWriter taskWriter,
        ITaskPipeline taskPipeline,
        IRetryPolicy retryPolicy,
        IProgressTracker progressTracker,
        ITaskLogger logger,
        IOptions<RunnerOptions> options)
    {
        _taskParser = taskParser;
        _taskWriter = taskWriter;
        _taskPipeline = taskPipeline;
        _retryPolicy = retryPolicy;
        _progressTracker = progressTracker;
        _logger = logger;
        _options = options.Value;
    }

    public async Task RunAllAsync(CancellationToken token = default)
    {
        var logScope = new TaskLogScope("Orchestrator", "RunAllSequential", 0);
        _logger.LogInfo(logScope, "Starting sequential orchestration...");

        if (_options.DryRun)
        {
            _logger.LogInfo(logScope, "Dry-run mode enabled. Tasks will be parsed but not executed.");
            var parsedPhases = await _taskParser.ParseAsync(_options.TasksFile, token);
            foreach (var phase in parsedPhases)
            {
                foreach (var task in phase.Tasks)
                {
                    _logger.LogInfo(logScope, $"Pending Task: {task.DisplayText}");
                }
            }
            return;
        }

        while (!token.IsCancellationRequested)
        {
            var phases = await _taskParser.ParseAsync(_options.TasksFile, token);
            var nextTask = _taskParser.GetNextTask(phases);

            if (nextTask == null)
            {
                _logger.LogInfo(logScope, "No pending tasks found. Orchestration complete.");
                break;
            }

            await RunSingleAsync(nextTask, token);
        }
    }

    public async Task RunSingleAsync(TaskItem task, CancellationToken token = default)
    {
        _progressTracker.ReportTaskStarted(task);

        // Update task to InProgress in markdown
        await _taskWriter.UpdateStatusAsync(_options.TasksFile, task, TaskStatus.InProgress, null, token);

        var result = await _retryPolicy.ExecuteAsync(async (attempt, ct) =>
        {
            // Execute the pipeline
            return await _taskPipeline.ExecuteAsync(task, ct);
        }, token);

        // Update task to Completed or Failed in markdown
        await _taskWriter.UpdateStatusAsync(_options.TasksFile, task, result.IsSuccess ? TaskStatus.Completed : TaskStatus.Failed, null, token);

        _progressTracker.ReportTaskCompleted(task, result.IsSuccess, result.Duration);
    }

    public Task StopAsync()
    {
        // Graceful shutdown can be handled by cancellation tokens from the host.
        return Task.CompletedTask;
    }
}
