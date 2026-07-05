namespace AntigravityTaskRunner.Terminal.Workspace;

/// <summary>
/// Analyzes the workspace to detect changes made by the terminal session.
/// </summary>
public interface IWorkspaceAnalyzer
{
    /// <summary>
    /// Takes a snapshot of the current workspace state.
    /// </summary>
    /// <param name="token">Cancellation token.</param>
    /// <returns>A snapshot of the workspace.</returns>
    Task<WorkspaceSnapshot> TakeSnapshotAsync(CancellationToken token = default);

    /// <summary>
    /// Compares two snapshots and returns the categorized change set: created,
    /// modified, deleted, and renamed files, each classified as meaningful (real
    /// implementation change) or not (documentation, cache, formatting-only).
    /// The tasks file, logs, and runner state are excluded.
    /// </summary>
    /// <param name="before">The snapshot taken before the task execution.</param>
    /// <param name="after">The snapshot taken after the task execution.</param>
    WorkspaceChangeSet GetChangeSet(WorkspaceSnapshot before, WorkspaceSnapshot after);
}
