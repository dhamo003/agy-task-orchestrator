using System;
using Runner.Markdown.Models;

namespace AntigravityTaskRunner.Core.Progress;

/// <summary>
/// Tracks progress of task execution.
/// </summary>
public interface IProgressTracker
{
    /// <summary>
    /// Event raised when a task starts.
    /// </summary>
    event EventHandler<TaskItem> TaskStarted;

    /// <summary>
    /// Event raised when a task completes.
    /// </summary>
    event EventHandler<(TaskItem Task, bool Success, TimeSpan Duration)> TaskCompleted;

    /// <summary>
    /// Reports that a task has started.
    /// </summary>
    void ReportTaskStarted(TaskItem task);

    /// <summary>
    /// Reports that a task has completed (successfully or failed).
    /// </summary>
    void ReportTaskCompleted(TaskItem task, bool success, TimeSpan duration);

    /// <summary>
    /// Gets the total number of tasks processed so far.
    /// </summary>
    int TasksProcessed { get; }

    /// <summary>
    /// Gets the number of successful tasks.
    /// </summary>
    int TasksSucceeded { get; }

    /// <summary>
    /// Gets the number of failed tasks.
    /// </summary>
    int TasksFailed { get; }
}
