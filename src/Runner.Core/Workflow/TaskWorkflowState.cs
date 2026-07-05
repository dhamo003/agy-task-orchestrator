namespace AntigravityTaskRunner.Core.Workflow;

/// <summary>
/// The finite set of workflow states a task moves through. The only legal forward
/// path is Pending → Running → Verifying → Completed; Verifying may loop back to
/// Running (retry), Running may move to Paused (capacity limit) and back, and both
/// Running and Verifying may terminate in Failed.
/// </summary>
public enum TaskWorkflowState
{
    /// <summary>Task selected but no session started yet.</summary>
    Pending,

    /// <summary>A fresh AI session is executing the task.</summary>
    Running,

    /// <summary>The attempt finished; workspace/build/test verification is in progress.</summary>
    Verifying,

    /// <summary>All verification checks passed; the task is done.</summary>
    Completed,

    /// <summary>The task failed permanently (retries exhausted or unrecoverable error).</summary>
    Failed,

    /// <summary>Execution paused due to a capacity limit; resumes on the same task.</summary>
    Paused
}
