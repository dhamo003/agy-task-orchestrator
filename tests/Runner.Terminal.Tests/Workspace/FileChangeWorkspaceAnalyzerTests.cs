using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Terminal.Workspace;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Runner.Terminal.Tests.Workspace;

public class FileChangeWorkspaceAnalyzerTests : IDisposable
{
    private readonly string _testWorkspacePath;
    private readonly RunnerOptions _options;
    
    public FileChangeWorkspaceAnalyzerTests()
    {
        _testWorkspacePath = Path.Combine(Path.GetTempPath(), "AntigravityTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testWorkspacePath);
        
        _options = new RunnerOptions
        {
            Workspace = new WorkspaceOptions
            {
                WorkspacePath = _testWorkspacePath,
                DetectStrategy = WorkspaceDetectStrategy.Timestamp,
                IncludePatterns = new List<string> { "**/*.txt" },
                ExcludePatterns = new List<string> { "**/ignore/**" }
            }
        };
    }

    [Fact]
    public async Task TakeSnapshot_ShouldIncludeOnlyMatchingFiles()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testWorkspacePath, "test1.txt"), "hello");
        File.WriteAllText(Path.Combine(_testWorkspacePath, "test2.log"), "hello");
        var ignoreDir = Path.Combine(_testWorkspacePath, "ignore");
        Directory.CreateDirectory(ignoreDir);
        File.WriteAllText(Path.Combine(ignoreDir, "test3.txt"), "hello");
        
        var analyzer = CreateAnalyzer();

        // Act
        var snapshot = await analyzer.TakeSnapshotAsync();

        // Assert
        Assert.Single(snapshot.Files);
        Assert.True(snapshot.Files.ContainsKey("test1.txt"));
    }

    [Fact]
    public void GetChanges_ShouldDetectAddedFiles()
    {
        // Arrange
        var analyzer = CreateAnalyzer();
        var snapshotBefore = new WorkspaceSnapshot(new Dictionary<string, FileSnapshot>(), DateTime.UtcNow);
        var snapshotAfter = new WorkspaceSnapshot(new Dictionary<string, FileSnapshot>
        {
            ["newfile.txt"] = new FileSnapshot("newfile.txt", DateTime.UtcNow, 100)
        }, DateTime.UtcNow);

        // Act
        var changes = analyzer.GetChanges(snapshotBefore, snapshotAfter);

        // Assert
        Assert.Single(changes);
        Assert.Contains("newfile.txt", changes);
    }

    [Fact]
    public void GetChanges_ShouldDetectModifiedFilesByTimestamp()
    {
        // Arrange
        var analyzer = CreateAnalyzer();
        var time1 = DateTime.UtcNow;
        var time2 = time1.AddSeconds(5);
        
        var snapshotBefore = new WorkspaceSnapshot(new Dictionary<string, FileSnapshot>
        {
            ["file.txt"] = new FileSnapshot("file.txt", time1, 100)
        }, DateTime.UtcNow);
        
        var snapshotAfter = new WorkspaceSnapshot(new Dictionary<string, FileSnapshot>
        {
            ["file.txt"] = new FileSnapshot("file.txt", time2, 100)
        }, DateTime.UtcNow);

        // Act
        var changes = analyzer.GetChanges(snapshotBefore, snapshotAfter);

        // Assert
        Assert.Single(changes);
        Assert.Contains("file.txt", changes);
    }
    
    [Fact]
    public void GetChanges_ShouldDetectDeletedFiles()
    {
        // Arrange
        var analyzer = CreateAnalyzer();
        var snapshotBefore = new WorkspaceSnapshot(new Dictionary<string, FileSnapshot>
        {
            ["deleted.txt"] = new FileSnapshot("deleted.txt", DateTime.UtcNow, 100)
        }, DateTime.UtcNow);
        var snapshotAfter = new WorkspaceSnapshot(new Dictionary<string, FileSnapshot>(), DateTime.UtcNow);

        // Act
        var changes = analyzer.GetChanges(snapshotBefore, snapshotAfter);

        // Assert
        Assert.Single(changes);
        Assert.Contains("deleted.txt", changes);
    }

    private FileChangeWorkspaceAnalyzer CreateAnalyzer()
    {
        return new FileChangeWorkspaceAnalyzer(Options.Create(_options), NullLogger<FileChangeWorkspaceAnalyzer>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testWorkspacePath))
        {
            Directory.Delete(_testWorkspacePath, true);
        }
        GC.SuppressFinalize(this);
    }
}
