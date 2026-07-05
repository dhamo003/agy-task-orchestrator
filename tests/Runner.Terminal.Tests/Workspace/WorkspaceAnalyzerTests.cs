using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Terminal.Workspace;

namespace AntigravityTaskRunner.Terminal.Tests.Workspace;

/// <summary>
/// Exercises the verification engine's change tracking against a real temp directory:
/// created/modified/deleted/renamed categorization, meaningful-diff detection
/// (comment/whitespace-only edits rejected), and tasks-file/cache exclusion.
/// </summary>
public sealed class WorkspaceAnalyzerTests : IDisposable
{
    private readonly string _dir;
    private readonly FileChangeWorkspaceAnalyzer _analyzer;

    public WorkspaceAnalyzerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ws-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);

        var options = new RunnerOptions { TasksFile = "tasks.md" };
        options.Workspace.WorkspacePath = _dir;
        options.Workspace.DetectStrategy = WorkspaceDetectStrategy.Hash;
        var ioptions = Microsoft.Extensions.Options.Options.Create(options);

        _analyzer = new FileChangeWorkspaceAnalyzer(
            ioptions,
            new SourceFileClassifier(ioptions),
            new Mock<ILogger<FileChangeWorkspaceAnalyzer>>().Object);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private void WriteFile(string relative, string content)
    {
        var path = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public async Task Detects_CreatedFile_AsMeaningful()
    {
        WriteFile("src/A.cs", "class A { }");
        var before = await _analyzer.TakeSnapshotAsync();

        WriteFile("src/B.cs", "class B { void M() { } }");
        var after = await _analyzer.TakeSnapshotAsync();

        var changes = _analyzer.GetChangeSet(before, after);
        changes.Created.Should().ContainSingle(c => c.Path.Contains("B.cs"));
        changes.HasMeaningfulChanges.Should().BeTrue();
    }

    [Fact]
    public async Task Detects_DeletedFile()
    {
        WriteFile("src/A.cs", "class A { }");
        WriteFile("src/B.cs", "class B { }");
        var before = await _analyzer.TakeSnapshotAsync();

        File.Delete(Path.Combine(_dir, "src/B.cs"));
        var after = await _analyzer.TakeSnapshotAsync();

        var changes = _analyzer.GetChangeSet(before, after);
        changes.Deleted.Should().ContainSingle(c => c.Path.Contains("B.cs"));
    }

    [Fact]
    public async Task Detects_Rename_ByContentHash()
    {
        WriteFile("src/OldName.cs", "class TheClass { void M() { } }");
        var before = await _analyzer.TakeSnapshotAsync();

        File.Move(Path.Combine(_dir, "src/OldName.cs"), Path.Combine(_dir, "src/NewName.cs"));
        var after = await _analyzer.TakeSnapshotAsync();

        var changes = _analyzer.GetChangeSet(before, after);
        var rename = changes.Renamed.Should().ContainSingle().Subject;
        rename.Path.Should().Contain("NewName.cs");
        rename.OldPath.Should().Contain("OldName.cs");
        changes.Created.Should().BeEmpty();
        changes.Deleted.Should().BeEmpty();
    }

    [Fact]
    public async Task RealCodeChange_IsMeaningful()
    {
        WriteFile("src/A.cs", "class A { int X() => 1; }");
        var before = await _analyzer.TakeSnapshotAsync();

        WriteFile("src/A.cs", "class A { int X() => 2; }");
        var after = await _analyzer.TakeSnapshotAsync();

        var changes = _analyzer.GetChangeSet(before, after);
        changes.Modified.Should().ContainSingle();
        changes.HasMeaningfulChanges.Should().BeTrue();
    }

    [Fact]
    public async Task CommentOnlyChange_IsNotMeaningful()
    {
        WriteFile("src/A.cs", "class A { int X() => 1; }");
        var before = await _analyzer.TakeSnapshotAsync();

        WriteFile("src/A.cs", "// a new comment\nclass A { int X() => 1; /* explain */ }");
        var after = await _analyzer.TakeSnapshotAsync();

        var changes = _analyzer.GetChangeSet(before, after);
        changes.Modified.Should().ContainSingle("the raw content did change");
        changes.HasMeaningfulChanges.Should().BeFalse("comments/whitespace are not implementation changes");
    }

    [Fact]
    public async Task WhitespaceOnlyChange_IsNotMeaningful()
    {
        WriteFile("src/A.cs", "class A { int X() => 1; }");
        var before = await _analyzer.TakeSnapshotAsync();

        WriteFile("src/A.cs", "class A\n{\n    int X() => 1;\n}\n");
        var after = await _analyzer.TakeSnapshotAsync();

        var changes = _analyzer.GetChangeSet(before, after);
        changes.HasMeaningfulChanges.Should().BeFalse("reformatting is not an implementation change");
    }

    [Fact]
    public async Task MarkdownChange_IsNotMeaningful()
    {
        WriteFile("README.md", "# Readme");
        var before = await _analyzer.TakeSnapshotAsync();

        WriteFile("README.md", "# Readme\n\nMore documentation.");
        var after = await _analyzer.TakeSnapshotAsync();

        var changes = _analyzer.GetChangeSet(before, after);
        changes.HasAnyChanges.Should().BeTrue();
        changes.HasMeaningfulChanges.Should().BeFalse();
    }

    [Fact]
    public async Task TasksFile_IsExcludedEntirely()
    {
        WriteFile("tasks.md", "- [ ] task");
        var before = await _analyzer.TakeSnapshotAsync();

        WriteFile("tasks.md", "- [x] task");
        var after = await _analyzer.TakeSnapshotAsync();

        var changes = _analyzer.GetChangeSet(before, after);
        changes.HasAnyChanges.Should().BeFalse("tasks.md is managed by the runner, not the agent");
    }

    [Fact]
    public async Task LogAndStateFiles_AreExcluded()
    {
        var before = await _analyzer.TakeSnapshotAsync();

        WriteFile("runner-state.json", "{}");
        WriteFile("output.log", "log line");
        var after = await _analyzer.TakeSnapshotAsync();

        var changes = _analyzer.GetChangeSet(before, after);
        changes.HasAnyChanges.Should().BeFalse();
    }

    [Fact]
    public async Task CompiledBinaries_AreExcludedEntirely()
    {
        // Regression: when launched from its publish folder, the runner once counted its
        // own re-published DLL/PDB files as workspace changes. Binaries are outputs,
        // never implementation.
        var before = await _analyzer.TakeSnapshotAsync();

        WriteFile("AntigravityTaskRunner.dll", "binary-v2");
        WriteFile("Runner.Core.pdb", "symbols");
        WriteFile("tool.exe", "exe");
        var after = await _analyzer.TakeSnapshotAsync();

        var changes = _analyzer.GetChangeSet(before, after);
        changes.HasAnyChanges.Should().BeFalse();
    }

    [Fact]
    public async Task ExtensionExclusion_DoesNotMisfireOnSimilarNames()
    {
        var before = await _analyzer.TakeSnapshotAsync();

        WriteFile("src/QueryExecutor.cs", "class QueryExecutor { int Run() => 1; }");
        var after = await _analyzer.TakeSnapshotAsync();

        var changes = _analyzer.GetChangeSet(before, after);
        changes.HasMeaningfulChanges.Should().BeTrue("'.exe' exclusion must not swallow 'Executor' source files");
    }
}
