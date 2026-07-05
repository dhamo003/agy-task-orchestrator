using System.Security.Cryptography;
using System.Text;
using AntigravityTaskRunner.Configuration;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AntigravityTaskRunner.Terminal.Workspace;

/// <summary>
/// Snapshot-diff based workspace analyzer. Content hashes (not timestamps) are the
/// source of truth so results are deterministic, and rename detection plus
/// normalized-content hashing let verification reject formatting-only edits.
/// </summary>
public class FileChangeWorkspaceAnalyzer : IWorkspaceAnalyzer
{
    /// <summary>Files larger than this are hashed but never content-normalized.</summary>
    private const long MaxNormalizableBytes = 2 * 1024 * 1024;

    private readonly WorkspaceOptions _options;
    private readonly string _tasksFileName;
    private readonly SourceFileClassifier _classifier;
    private readonly ILogger<FileChangeWorkspaceAnalyzer> _logger;
    private readonly Matcher _matcher;

    public FileChangeWorkspaceAnalyzer(
        IOptions<RunnerOptions> options,
        SourceFileClassifier classifier,
        ILogger<FileChangeWorkspaceAnalyzer> logger)
    {
        _options = options.Value.Workspace;
        _tasksFileName = Path.GetFileName(options.Value.TasksFile);
        _classifier = classifier;
        _logger = logger;

        _matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        foreach (var pattern in _options.IncludePatterns)
        {
            _matcher.AddInclude(pattern);
        }
        foreach (var pattern in _options.ExcludePatterns)
        {
            _matcher.AddExclude(pattern);
        }
    }

    public async Task<WorkspaceSnapshot> TakeSnapshotAsync(CancellationToken token = default)
    {
        var rootDir = new DirectoryInfo(_options.WorkspacePath);
        if (!rootDir.Exists)
        {
            _logger.LogWarning("Workspace path does not exist: {Path}", _options.WorkspacePath);
            return new WorkspaceSnapshot(new Dictionary<string, FileSnapshot>(), DateTime.UtcNow);
        }

        var files = new Dictionary<string, FileSnapshot>(StringComparer.OrdinalIgnoreCase);
        var matches = _matcher.GetResultsInFullPath(rootDir.FullName);

        foreach (var filePath in matches)
        {
            token.ThrowIfCancellationRequested();

            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists) continue; // Might have been deleted concurrently

            var relPath = Path.GetRelativePath(rootDir.FullName, fileInfo.FullName);

            string? hash = null;
            string? normalizedHash = null;
            if (_options.DetectStrategy is WorkspaceDetectStrategy.Hash or WorkspaceDetectStrategy.Both)
            {
                (hash, normalizedHash) = await ComputeHashesAsync(fileInfo, relPath, token);
            }

            files[relPath] = new FileSnapshot(
                relPath,
                fileInfo.LastWriteTimeUtc,
                fileInfo.Length,
                hash,
                normalizedHash
            );
        }

        return new WorkspaceSnapshot(files, DateTime.UtcNow);
    }

    public WorkspaceChangeSet GetChangeSet(WorkspaceSnapshot before, WorkspaceSnapshot after)
    {
        var changes = new List<FileChange>();
        var created = new List<FileSnapshot>();
        var deleted = new List<FileSnapshot>();

        foreach (var (path, afterFile) in after.Files)
        {
            if (IsExcludedFromVerification(path)) continue;

            if (!before.Files.TryGetValue(path, out var beforeFile))
            {
                created.Add(afterFile);
            }
            else if (ContentDiffers(beforeFile, afterFile))
            {
                changes.Add(new FileChange(path, FileChangeKind.Modified,
                    IsMeaningful: IsModificationMeaningful(beforeFile, afterFile)));
            }
        }

        foreach (var (path, beforeFile) in before.Files)
        {
            if (IsExcludedFromVerification(path)) continue;

            if (!after.Files.ContainsKey(path))
            {
                deleted.Add(beforeFile);
            }
        }

        // Rename detection: a deleted file whose content hash matches a created file.
        var unmatchedCreated = new List<FileSnapshot>(created);
        foreach (var del in deleted)
        {
            FileSnapshot? match = del.Hash is null
                ? null
                : unmatchedCreated.FirstOrDefault(c => c.Hash == del.Hash);

            if (match is not null)
            {
                unmatchedCreated.Remove(match);
                changes.Add(new FileChange(match.RelativePath, FileChangeKind.Renamed,
                    OldPath: del.RelativePath,
                    IsMeaningful: IsImplementationFile(match.RelativePath)));
            }
            else
            {
                changes.Add(new FileChange(del.RelativePath, FileChangeKind.Deleted,
                    IsMeaningful: IsImplementationFile(del.RelativePath)));
            }
        }

        foreach (var add in unmatchedCreated)
        {
            changes.Add(new FileChange(add.RelativePath, FileChangeKind.Created,
                IsMeaningful: IsImplementationFile(add.RelativePath)));
        }

        return new WorkspaceChangeSet(changes);
    }

    private bool IsExcludedFromVerification(string relativePath) =>
        _classifier.IsIgnored(relativePath) ||
        string.Equals(Path.GetFileName(relativePath), _tasksFileName, StringComparison.OrdinalIgnoreCase);

    private bool IsImplementationFile(string relativePath) =>
        _classifier.IsSource(relativePath) && !_classifier.IsDocumentation(relativePath);

    private bool ContentDiffers(FileSnapshot before, FileSnapshot after)
    {
        if (_options.DetectStrategy is WorkspaceDetectStrategy.Hash or WorkspaceDetectStrategy.Both
            && before.Hash is not null && after.Hash is not null)
        {
            return before.Hash != after.Hash;
        }

        return before.LastWriteTimeUtc != after.LastWriteTimeUtc || before.Length != after.Length;
    }

    private bool IsModificationMeaningful(FileSnapshot before, FileSnapshot after)
    {
        if (!IsImplementationFile(after.RelativePath))
        {
            return false;
        }

        // When both normalized hashes are available, a modification is meaningful only
        // when the normalized (comment/whitespace-stripped) content actually changed.
        if (before.NormalizedHash is not null && after.NormalizedHash is not null)
        {
            return before.NormalizedHash != after.NormalizedHash;
        }

        return true; // Fall back: raw content differs and file is a source file.
    }

    private async Task<(string? Hash, string? NormalizedHash)> ComputeHashesAsync(
        FileInfo fileInfo, string relativePath, CancellationToken token)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(fileInfo.FullName, token);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));

            string? normalizedHash = null;
            if (fileInfo.Length <= MaxNormalizableBytes && _classifier.IsSource(relativePath))
            {
                var content = Encoding.UTF8.GetString(bytes);
                normalizedHash = _classifier.ComputeNormalizedHash(relativePath, content);
            }

            return (hash, normalizedHash);
        }
        catch (IOException)
        {
            // If the file is locked, assume it changed: return a unique value.
            return (Guid.NewGuid().ToString(), null);
        }
    }
}
