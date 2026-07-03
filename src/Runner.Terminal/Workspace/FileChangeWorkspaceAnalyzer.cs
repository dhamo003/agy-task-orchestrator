using System.Security.Cryptography;
using AntigravityTaskRunner.Configuration;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AntigravityTaskRunner.Terminal.Workspace;

/// <summary>
/// Analyzes workspace files based on timestamps and/or hashes to detect changes.
/// </summary>
public class FileChangeWorkspaceAnalyzer : IWorkspaceAnalyzer
{
    private readonly WorkspaceOptions _options;
    private readonly ILogger<FileChangeWorkspaceAnalyzer> _logger;
    private readonly Matcher _matcher;

    public FileChangeWorkspaceAnalyzer(IOptions<RunnerOptions> options, ILogger<FileChangeWorkspaceAnalyzer> logger)
    {
        _options = options.Value.Workspace;
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
            if (_options.DetectStrategy is WorkspaceDetectStrategy.Hash or WorkspaceDetectStrategy.Both)
            {
                hash = await ComputeHashAsync(fileInfo.FullName, token);
            }

            files[relPath] = new FileSnapshot(
                relPath,
                fileInfo.LastWriteTimeUtc,
                fileInfo.Length,
                hash
            );
        }

        return new WorkspaceSnapshot(files, DateTime.UtcNow);
    }

    public IReadOnlyList<string> GetChanges(WorkspaceSnapshot before, WorkspaceSnapshot after)
    {
        var changedFiles = new List<string>();

        // Check for added or modified files
        foreach (var (path, afterFile) in after.Files)
        {
            if (!before.Files.TryGetValue(path, out var beforeFile))
            {
                changedFiles.Add(path); // Added
            }
            else
            {
                if (_options.DetectStrategy is WorkspaceDetectStrategy.Hash or WorkspaceDetectStrategy.Both)
                {
                    if (beforeFile.Hash != afterFile.Hash)
                    {
                        changedFiles.Add(path); // Modified by hash
                        continue;
                    }
                }
                
                if (_options.DetectStrategy is WorkspaceDetectStrategy.Timestamp or WorkspaceDetectStrategy.Both)
                {
                    if (beforeFile.LastWriteTimeUtc != afterFile.LastWriteTimeUtc || beforeFile.Length != afterFile.Length)
                    {
                        changedFiles.Add(path); // Modified by timestamp/size
                    }
                }
            }
        }

        // Check for deleted files
        foreach (var (path, _) in before.Files)
        {
            if (!after.Files.ContainsKey(path))
            {
                changedFiles.Add(path); // Deleted
            }
        }

        return changedFiles;
    }

    private static async Task<string> ComputeHashAsync(string filePath, CancellationToken token)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var sha256 = SHA256.Create();
            var hashBytes = await sha256.ComputeHashAsync(stream, token);
            return Convert.ToHexString(hashBytes);
        }
        catch (IOException)
        {
            // If the file is locked, just return a unique value or null. 
            // Better to assume it changed if we can't read it.
            return Guid.NewGuid().ToString();
        }
    }
}
