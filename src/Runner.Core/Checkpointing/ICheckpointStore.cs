using System.Threading;
using System.Threading.Tasks;
using AntigravityTaskRunner.Terminal.Workspace;

namespace AntigravityTaskRunner.Core.Checkpointing;

/// <summary>
/// Persists and restores execution checkpoints (and the pre-task workspace snapshot)
/// so a crashed or paused run resumes from the exact same point.
/// </summary>
public interface ICheckpointStore
{
    Task SaveAsync(ExecutionCheckpoint checkpoint, CancellationToken token = default);

    Task SaveSnapshotAsync(WorkspaceSnapshot snapshot, CancellationToken token = default);

    Task<ExecutionCheckpoint?> LoadAsync(CancellationToken token = default);

    Task<WorkspaceSnapshot?> LoadSnapshotAsync(CancellationToken token = default);

    Task ClearAsync(CancellationToken token = default);
}
