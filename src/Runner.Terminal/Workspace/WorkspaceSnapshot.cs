namespace AntigravityTaskRunner.Terminal.Workspace;

/// <summary>
/// Snapshot of a single file.
/// </summary>
public sealed record FileSnapshot(
    string RelativePath,
    DateTime LastWriteTimeUtc,
    long Length,
    string? Hash = null
);

/// <summary>
/// A snapshot of the workspace at a specific point in time.
/// </summary>
/// <param name="Files">A dictionary mapping relative file paths to their snapshots.</param>
/// <param name="CapturedAtUtc">The time the snapshot was taken.</param>
public sealed record WorkspaceSnapshot(
    IReadOnlyDictionary<string, FileSnapshot> Files,
    DateTime CapturedAtUtc
);
