namespace AntigravityTaskRunner.Configuration;

/// <summary>
/// Configuration for persistent execution checkpointing / crash recovery.
/// Binds to the "Runner:Checkpoint" section in appsettings.json.
/// </summary>
public sealed class CheckpointOptions
{
    /// <summary>Master switch. When false no checkpoint is written or resumed.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Directory (relative to the workspace, or absolute) where checkpoint state is stored.
    /// </summary>
    public string Directory { get; set; } = ".antigravity";

    /// <summary>Checkpoint file name inside <see cref="Directory"/>.</summary>
    public string CheckpointFileName { get; set; } = "checkpoint.json";

    /// <summary>Workspace snapshot file name inside <see cref="Directory"/>.</summary>
    public string SnapshotFileName { get; set; } = "workspace-snapshot.json";
}
