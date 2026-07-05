using System;
using System.Threading;
using AntigravityTaskRunner.Core.Models;
using Runner.Markdown.Models;

namespace AntigravityTaskRunner.Core.Progress;

/// <summary>
/// Thread-safe implementation of IProgressTracker.
/// </summary>
public class ProgressTracker : IProgressTracker
{
    private int _tasksProcessed;
    private int _tasksSucceeded;
    private int _tasksFailed;

    public int TasksProcessed => _tasksProcessed;
    public int TasksSucceeded => _tasksSucceeded;
    public int TasksFailed => _tasksFailed;

    public event EventHandler<TaskItem>? TaskStarted;
    public event EventHandler<(TaskItem Task, bool Success, TimeSpan Duration)>? TaskCompleted;
    public event EventHandler<(TaskItem Task, string Status)>? StatusChanged;
    public event EventHandler<PipelineHaltReport>? PipelineHalted;

    public void ReportTaskStarted(TaskItem task)
    {
        TaskStarted?.Invoke(this, task);
    }

    public void ReportTaskCompleted(TaskItem task, bool success, TimeSpan duration)
    {
        Interlocked.Increment(ref _tasksProcessed);
        if (success)
        {
            Interlocked.Increment(ref _tasksSucceeded);
        }
        else
        {
            Interlocked.Increment(ref _tasksFailed);
        }

        TaskCompleted?.Invoke(this, (task, success, duration));
    }

    public void ReportStatus(TaskItem task, string status)
    {
        StatusChanged?.Invoke(this, (task, status));
    }

    public void ReportPipelineHalted(PipelineHaltReport report)
    {
        PipelineHalted?.Invoke(this, report);
    }
}
