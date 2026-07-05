namespace AntigravityTaskRunner.Configuration;

/// <summary>
/// Defines the strategy for detecting workspace changes after task execution.
/// </summary>
public enum WorkspaceDetectStrategy
{
    /// <summary>
    /// Detect changes by comparing file modification timestamps.
    /// </summary>
    Timestamp,

    /// <summary>
    /// Detect changes by computing and comparing file content hashes.
    /// </summary>
    Hash,

    /// <summary>
    /// Detect changes using both timestamp and hash (most thorough).
    /// </summary>
    Both,

    /// <summary>
    /// Skip workspace change detection entirely.
    /// </summary>
    None
}

/// <summary>
/// Configuration for workspace analysis and change detection.
/// Binds to the "Runner:Workspace" section in appsettings.json.
/// </summary>
public sealed class WorkspaceOptions
{
    /// <summary>
    /// Root path of the workspace to monitor for changes.
    /// Defaults to the current directory.
    /// </summary>
    public string WorkspacePath { get; set; } = ".";

    /// <summary>
    /// Path to the solution file (.sln/.slnx) within the workspace, if applicable.
    /// Used for build verification.
    /// </summary>
    public string? SolutionFile { get; set; }

    /// <summary>
    /// Strategy for detecting workspace changes after task execution.
    /// Content hashing is the default: deterministic and immune to timestamp noise.
    /// </summary>
    public WorkspaceDetectStrategy DetectStrategy { get; set; } = WorkspaceDetectStrategy.Hash;

    /// <summary>
    /// File patterns to include when scanning workspace changes (glob patterns).
    /// Everything is snapshotted by default; the verification engine decides which
    /// changes are meaningful.
    /// </summary>
    public List<string> IncludePatterns { get; set; } = ["**/*"];

    /// <summary>
    /// File patterns to exclude when scanning workspace changes (glob patterns).
    /// </summary>
    public List<string> ExcludePatterns { get; set; } =
        ["**/bin/**", "**/obj/**", "**/.git/**", "**/.vs/**", "**/node_modules/**",
         "**/logs/**", "**/.antigravity/**"];
}
