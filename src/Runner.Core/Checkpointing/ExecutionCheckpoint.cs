using System;
using System.Collections.Generic;
using AntigravityTaskRunner.Core.Models;
using AntigravityTaskRunner.Core.Workflow;

namespace AntigravityTaskRunner.Core.Checkpointing;

/// <summary>One historical attempt entry preserved in the checkpoint.</summary>
public sealed record AttemptRecord(
    int Attempt,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    bool Success,
    FailureKind Failure,
    string? ErrorMessage);

/// <summary>
/// The complete persisted execution state for crash recovery: which task is active,
/// its workflow state, retry count, the prompt and model in use, files modified so
/// far, and the full attempt history. Together with the persisted workspace snapshot
/// this allows resuming the exact same task from the exact same baseline.
/// </summary>
public sealed record ExecutionCheckpoint(
    int TaskLine,
    string TaskText,
    TaskWorkflowState State,
    int Attempt,
    int PauseCount,
    string? Prompt,
    string Model,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<string> ModifiedFiles,
    IReadOnlyList<AttemptRecord> History,
    IReadOnlyList<string>? BaselineFailedTests = null)
{
    public static ExecutionCheckpoint Start(int taskLine, string taskText, string model) =>
        new(taskLine, taskText, TaskWorkflowState.Pending, Attempt: 1, PauseCount: 0,
            Prompt: null, Model: model,
            StartedAtUtc: DateTimeOffset.UtcNow, UpdatedAtUtc: DateTimeOffset.UtcNow,
            ModifiedFiles: [], History: []);

    /// <summary>
    /// True when this checkpoint refers to the same checklist task. Compared on the
    /// display text (not the raw line) because the checkbox marker changes as the
    /// task moves through [ ] → [/] → [x]/[!].
    /// </summary>
    public bool Matches(int taskLine, string taskDisplayText) =>
        TaskLine == taskLine &&
        string.Equals(TaskText?.Trim(), taskDisplayText?.Trim(), StringComparison.Ordinal);
}
