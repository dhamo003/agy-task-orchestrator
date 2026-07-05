using System;
using AntigravityTaskRunner.Core.Models;
using Runner.Markdown.Models;

namespace AntigravityTaskRunner.Core.Progress;

/// <summary>
/// Tracks progress of task execution, including live per-phase status so the UI never
/// appears frozen during long AI processing, verification, build, or test phases.
/// </summary>
public interface IProgressTracker
{
    /// <summary>Raised when a task starts.</summary>
    event EventHandler<TaskItem> TaskStarted;

    /// <summary>Raised when a task completes.</summary>
    event EventHandler<(TaskItem Task, bool Success, TimeSpan Duration)> TaskCompleted;

    /// <summary>
    /// Raised whenever the live execution status changes (phase transitions, AI
    /// heartbeats, verification/build/test progress). Carries a display string.
    /// </summary>
    event EventHandler<(TaskItem Task, string Status)>? StatusChanged;

    /// <summary>Raised when the pipeline halts permanently on an unrecoverable failure.</summary>
    event EventHandler<PipelineHaltReport>? PipelineHalted;

    void ReportTaskStarted(TaskItem task);

    void ReportTaskCompleted(TaskItem task, bool success, TimeSpan duration);

    /// <summary>Reports a live status update for the currently-running task.</summary>
    void ReportStatus(TaskItem task, string status);

    /// <summary>Reports that the pipeline stopped permanently because of <paramref name="report"/>.</summary>
    void ReportPipelineHalted(PipelineHaltReport report);

    int TasksProcessed { get; }
    int TasksSucceeded { get; }
    int TasksFailed { get; }
}
