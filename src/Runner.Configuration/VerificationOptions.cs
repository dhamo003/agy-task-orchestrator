namespace AntigravityTaskRunner.Configuration;

/// <summary>
/// Configuration for the completion-verification engine that decides whether an
/// attempt produced a real implementation change.
/// Binds to the "Runner:Verification" section in appsettings.json.
/// </summary>
public sealed class VerificationOptions
{
    /// <summary>
    /// File extensions (lower-case, with dot) that count as implementation/source files.
    /// A task is only considered complete when at least one of these changed meaningfully.
    /// </summary>
    public List<string> SourceExtensions { get; set; } =
        [".cs", ".csproj", ".sln", ".slnx", ".props", ".targets", ".json", ".xml",
         ".razor", ".cshtml", ".ts", ".tsx", ".js", ".jsx", ".py", ".sql", ".yaml", ".yml"];

    /// <summary>
    /// File extensions that are always ignored by verification (documentation-only or
    /// noise). Changes limited to these files are rejected as non-implementation.
    /// </summary>
    public List<string> DocumentationExtensions { get; set; } = [".md", ".markdown", ".txt", ".rst"];

    /// <summary>
    /// Path fragments that mark a file as cache/derived output. Changes to these never
    /// count as implementation changes.
    /// </summary>
    public List<string> IgnoredPathFragments { get; set; } =
        ["/bin/", "/obj/", "/.git/", "/.vs/", "/node_modules/", "/logs/", "/.antigravity/",
         "runner-state.json", ".cache"];

    /// <summary>
    /// File extensions (exact match, lower-case with dot) excluded from verification
    /// entirely: compiled binaries and tool noise are outputs, never implementation.
    /// </summary>
    public List<string> IgnoredExtensions { get; set; } =
        [".dll", ".pdb", ".exe", ".log", ".suo", ".user", ".baml", ".ide"];

    /// <summary>
    /// When true, a modified source file only counts as a meaningful change when its
    /// comment/whitespace-normalized content actually differs (rejects formatting-only edits).
    /// </summary>
    public bool RequireMeaningfulDiff { get; set; } = true;

    /// <summary>
    /// When true, the TASK_COMPLETED marker must have been observed for a task to complete.
    /// </summary>
    public bool RequireCompletionMarker { get; set; } = true;
}
