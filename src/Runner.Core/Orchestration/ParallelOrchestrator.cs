using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
/// Orchestrator that runs tasks concurrently using a semaphore.
/// </summary>
public class ParallelOrchestrator : ITaskOrchestrator
{
    private readonly ITaskParser _taskParser;
    private readonly ITaskWriter _taskWriter;
    private readonly ITaskPipeline _taskPipeline;
    private readonly IRetryPolicy _retryPolicy;
    private readonly IProgressTracker _progressTracker;
    private readonly ITaskLogger _logger;
    private readonly RunnerOptions _options;

    public ParallelOrchestrator(
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
        var logScope = new TaskLogScope("Orchestrator", "RunAllParallel", 0);
        _logger.LogInfo(logScope, "Starting parallel orchestration...");

        if (_options.DryRun)
        {
            _logger.LogInfo(logScope, "Dry-run mode enabled. Tasks will be parsed but not executed.");
            var parsedPhases = await _taskParser.ParseAsync(_options.TasksFile, token);
            foreach (var phase in parsedPhases)
            {
                foreach (var t in phase.Tasks)
                {
                    _logger.LogInfo(logScope, $"Pending Task: {t.DisplayText}");
                }
            }
            return;
        }

        // Parse all phases to find pending tasks. In parallel mode, we might dispatch all available tasks
        // across phases, or maybe just within the current phase. The requirement doesn't specify strictly,
        // but typically we'd extract all pending tasks and queue them.
        
        var phases = await _taskParser.ParseAsync(_options.TasksFile, token);
        var pendingTasks = phases.SelectMany(p => p.Tasks)
            .Where(t => t.Status == Runner.Markdown.Models.TaskStatus.NotStarted || t.Status == Runner.Markdown.Models.TaskStatus.InProgress)
            .ToList();

        if (pendingTasks.Count == 0)
        {
            _logger.LogInfo(logScope, "No pending tasks found. Orchestration complete.");
            return;
        }

        int maxWorkers = Math.Max(1, _options.Parallel.MaxWorkers);
        using var semaphore = new SemaphoreSlim(maxWorkers);
        var executingTasks = new List<Task>();

        foreach (var task in pendingTasks)
        {
            if (token.IsCancellationRequested)
                break;

            await semaphore.WaitAsync(token);

            var execTask = Task.Run(async () =>
            {
                try
                {
                    await RunSingleAsync(task, token);
                }
                finally
                {
                    semaphore.Release();
                }
            }, token);

            executingTasks.Add(execTask);
        }

        await Task.WhenAll(executingTasks);
    }

    public async Task RunSingleAsync(TaskItem task, CancellationToken token = default)
    {
        _progressTracker.ReportTaskStarted(task);

        await _taskWriter.UpdateStatusAsync(_options.TasksFile, task, TaskStatus.InProgress, null, token);

        var result = await _retryPolicy.ExecuteAsync(async (attempt, ct) =>
        {
            return await _taskPipeline.ExecuteAsync(task, ct);
        }, token);

        await _taskWriter.UpdateStatusAsync(_options.TasksFile, task, result.IsSuccess ? TaskStatus.Completed : TaskStatus.Failed, null, token);

        _progressTracker.ReportTaskCompleted(task, result.IsSuccess, result.Duration);
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }
}
