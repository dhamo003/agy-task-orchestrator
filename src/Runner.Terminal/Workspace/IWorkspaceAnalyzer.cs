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
    /// Compares two snapshots and returns a list of changed files (added, modified, deleted).
    /// </summary>
    /// <param name="before">The snapshot taken before the task execution.</param>
    /// <param name="after">The snapshot taken after the task execution.</param>
    /// <returns>A list of relative file paths that have changed.</returns>
    IReadOnlyList<string> GetChanges(WorkspaceSnapshot before, WorkspaceSnapshot after);
}
