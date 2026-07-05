namespace AntigravityTaskRunner.Terminal.Workspace;

/// <summary>
/// Snapshot of a single file.
/// </summary>
/// <param name="RelativePath">Path relative to the workspace root.</param>
/// <param name="LastWriteTimeUtc">Last write timestamp.</param>
/// <param name="Length">File size in bytes.</param>
/// <param name="Hash">SHA-256 of the raw content (hex), when hashing is enabled.</param>
/// <param name="NormalizedHash">
/// SHA-256 of the comment/whitespace-normalized content for source files. Two files
/// with equal normalized hashes differ at most in comments, whitespace, or formatting.
/// Null for non-source or unreadable files.
/// </param>
public sealed record FileSnapshot(
    string RelativePath,
    DateTime LastWriteTimeUtc,
    long Length,
    string? Hash = null,
    string? NormalizedHash = null
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
