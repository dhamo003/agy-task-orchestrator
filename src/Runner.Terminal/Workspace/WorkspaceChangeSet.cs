using System.Linq;

namespace AntigravityTaskRunner.Terminal.Workspace;

/// <summary>The kind of change detected for a single file.</summary>
public enum FileChangeKind
{
    Created,
    Modified,
    Deleted,
    Renamed
}

/// <summary>
/// A single detected file change.
/// </summary>
/// <param name="Path">Relative path of the file (new path for renames).</param>
/// <param name="Kind">The change kind.</param>
/// <param name="OldPath">The previous path when <paramref name="Kind"/> is Renamed.</param>
/// <param name="IsMeaningful">
/// True when the change represents a real implementation change (source file whose
/// normalized content actually differs), as opposed to documentation, cache noise,
/// or comment/whitespace-only edits.
/// </param>
public sealed record FileChange(
    string Path,
    FileChangeKind Kind,
    string? OldPath = null,
    bool IsMeaningful = false);

/// <summary>
/// The categorized result of comparing two workspace snapshots.
/// </summary>
public sealed record WorkspaceChangeSet(IReadOnlyList<FileChange> Changes)
{
    public static WorkspaceChangeSet Empty { get; } = new([]);

    public IEnumerable<FileChange> Created => Changes.Where(c => c.Kind == FileChangeKind.Created);
    public IEnumerable<FileChange> Modified => Changes.Where(c => c.Kind == FileChangeKind.Modified);
    public IEnumerable<FileChange> Deleted => Changes.Where(c => c.Kind == FileChangeKind.Deleted);
    public IEnumerable<FileChange> Renamed => Changes.Where(c => c.Kind == FileChangeKind.Renamed);

    /// <summary>All changes that count as real implementation changes.</summary>
    public IReadOnlyList<FileChange> MeaningfulChanges => Changes.Where(c => c.IsMeaningful).ToList();

    /// <summary>True when at least one real implementation change exists.</summary>
    public bool HasMeaningfulChanges => Changes.Any(c => c.IsMeaningful);

    /// <summary>True when any change (meaningful or not) exists.</summary>
    public bool HasAnyChanges => Changes.Count > 0;

    public override string ToString() =>
        $"{Changes.Count} change(s): {Created.Count()} created, {Modified.Count()} modified, " +
        $"{Deleted.Count()} deleted, {Renamed.Count()} renamed; {MeaningfulChanges.Count} meaningful";
}
