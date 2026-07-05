using System;
using Xunit;
using FluentAssertions;
using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Core.Verification;
using AntigravityTaskRunner.Terminal.Sessions;
using AntigravityTaskRunner.Terminal.Workspace;

namespace AntigravityTaskRunner.Core.Tests;

public class CompletionVerifierTests
{
    private static CompletionVerifier Build(Action<VerificationOptions>? configure = null)
    {
        var options = new RunnerOptions();
        configure?.Invoke(options.Verification);
        return new CompletionVerifier(Microsoft.Extensions.Options.Options.Create(options));
    }

    private static AgentRunResult Completed() =>
        new("TASK_COMPLETED", true, true, null, null, false);

    [Fact]
    public void Passes_WithMarkerAndMeaningfulChange()
    {
        var report = Build().Verify(Completed(),
            new WorkspaceChangeSet([new FileChange("src/A.cs", FileChangeKind.Modified, IsMeaningful: true)]));

        report.Passed.Should().BeTrue();
        report.Checks.Should().OnlyContain(c => c.Passed);
    }

    [Fact]
    public void Fails_WhenMarkerMissing_EvenWithChanges()
    {
        var report = Build().Verify(
            new AgentRunResult("done!", false, false, null, null, false),
            new WorkspaceChangeSet([new FileChange("src/A.cs", FileChangeKind.Created, IsMeaningful: true)]));

        report.Passed.Should().BeFalse();
        report.FailedChecks.Should().Contain(c => c.Name == "CompletionMarker");
    }

    [Fact]
    public void Fails_WhenOnlyDocumentationChanged()
    {
        var report = Build().Verify(Completed(),
            new WorkspaceChangeSet([new FileChange("docs/notes.md", FileChangeKind.Modified, IsMeaningful: false)]));

        report.Passed.Should().BeFalse();
        report.FailedChecks.Should().Contain(c => c.Name == "MeaningfulImplementationDiff");
    }

    [Fact]
    public void Fails_WhenNoChangesAtAll()
    {
        var report = Build().Verify(Completed(), WorkspaceChangeSet.Empty);

        report.Passed.Should().BeFalse();
        report.FailedChecks.Should().Contain(c => c.Name == "SourceFilesModified");
    }

    [Fact]
    public void Fails_OnExplicitFailureMarker()
    {
        var report = Build().Verify(
            new AgentRunResult("TASK_FAILED: nope", true, false, "TASK_FAILED: nope", null, false),
            new WorkspaceChangeSet([new FileChange("src/A.cs", FileChangeKind.Modified, IsMeaningful: true)]));

        report.Passed.Should().BeFalse();
        report.FailedChecks.Should().Contain(c => c.Detail.Contains("TASK_FAILED"));
    }

    [Fact]
    public void MeaningfulDiffCheck_CanBeDisabled()
    {
        var verifier = Build(v => v.RequireMeaningfulDiff = false);
        var report = verifier.Verify(Completed(),
            new WorkspaceChangeSet([new FileChange("docs/notes.md", FileChangeKind.Modified, IsMeaningful: false)]));

        report.Passed.Should().BeTrue("with RequireMeaningfulDiff=false any change suffices");
    }

    [Fact]
    public void Describe_ListsEveryCheck()
    {
        var report = Build().Verify(Completed(), WorkspaceChangeSet.Empty);
        var text = report.Describe();
        text.Should().Contain("FAILED").And.Contain("SourceFilesModified");
    }
}
