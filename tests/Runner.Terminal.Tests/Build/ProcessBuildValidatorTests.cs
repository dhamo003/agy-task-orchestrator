using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Terminal.Build;

namespace AntigravityTaskRunner.Terminal.Tests.Build;

public sealed class ProcessBuildValidatorTests : IDisposable
{
    private readonly string _dir;

    public ProcessBuildValidatorTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "build-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private sealed class FakeRunner : IProcessCommandRunner
    {
        private readonly Func<string, IReadOnlyList<string>, CommandResult> _behavior;
        public List<string> StagesRun { get; } = [];

        public FakeRunner(Func<string, IReadOnlyList<string>, CommandResult> behavior) => _behavior = behavior;

        public Task<CommandResult> RunAsync(string command, IReadOnlyList<string> arguments,
            string workingDirectory, TimeSpan timeout, CancellationToken token = default)
        {
            StagesRun.Add(string.Join(' ', arguments));
            return Task.FromResult(_behavior(command, arguments));
        }
    }

    private ProcessBuildValidator Build(FakeRunner runner, Action<BuildValidationOptions>? configure = null)
    {
        var options = new RunnerOptions { WorkspacePath = _dir };
        configure?.Invoke(options.Build);
        return new ProcessBuildValidator(
            Microsoft.Extensions.Options.Options.Create(options),
            runner,
            new Mock<ILogger<ProcessBuildValidator>>().Object);
    }

    private void AddProject(string name = "App.csproj", string content = "<Project Sdk=\"Microsoft.NET.Sdk\" />") =>
        File.WriteAllText(Path.Combine(_dir, name), content);

    [Fact]
    public async Task AllStagesPass_ReturnsSuccess()
    {
        AddProject();
        var runner = new FakeRunner((_, _) => new CommandResult(0, "ok", false));

        var result = await Build(runner).ValidateAsync();

        result.Success.Should().BeTrue();
        result.Skipped.Should().BeFalse();
        runner.StagesRun.Should().HaveCount(2, "restore + build run; test is skipped (no test projects)");
    }

    [Fact]
    public async Task FailingBuild_StopsAtFirstFailure_AndCapturesOutput()
    {
        AddProject();
        var runner = new FakeRunner((_, args) =>
            args.Contains("build")
                ? new CommandResult(1, "error CS1002: ; expected", false)
                : new CommandResult(0, "ok", false));

        var result = await Build(runner).ValidateAsync();

        result.Success.Should().BeFalse();
        result.FirstFailure!.Name.Should().Be("build");
        result.FirstFailure.Output.Should().Contain("CS1002");
        runner.StagesRun.Should().HaveCount(2, "the test stage must not run after a failed build");
    }

    [Fact]
    public async Task TestStage_Runs_WhenTestProjectsExist()
    {
        AddProject("App.csproj");
        AddProject("App.Tests.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><PackageReference Include=\"Microsoft.NET.Test.Sdk\" /></ItemGroup></Project>");
        var runner = new FakeRunner((_, _) => new CommandResult(0, "ok", false));

        var result = await Build(runner).ValidateAsync();

        result.Success.Should().BeTrue();
        runner.StagesRun.Should().HaveCount(3, "restore + build + test");
    }

    [Fact]
    public async Task NoProject_SkipsValidation_WhenConfigured()
    {
        var runner = new FakeRunner((_, _) => new CommandResult(0, "ok", false));

        var result = await Build(runner).ValidateAsync();

        result.Success.Should().BeTrue();
        result.Skipped.Should().BeTrue();
        runner.StagesRun.Should().BeEmpty();
    }

    [Fact]
    public async Task NoProject_Fails_WhenSkipDisabled()
    {
        var runner = new FakeRunner((_, _) => new CommandResult(0, "ok", false));

        var result = await Build(runner, b => b.SkipWhenNoProject = false).ValidateAsync();

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Timeout_FailsTheStage()
    {
        AddProject();
        var runner = new FakeRunner((_, _) => new CommandResult(-1, "partial output", TimedOut: true));

        var result = await Build(runner).ValidateAsync();

        result.Success.Should().BeFalse();
        result.FirstFailure!.Output.Should().Contain("timed out");
    }

    [Fact]
    public async Task Disabled_SkipsEverything()
    {
        AddProject();
        var runner = new FakeRunner((_, _) => new CommandResult(0, "ok", false));

        var result = await Build(runner, b => b.Enabled = false).ValidateAsync();

        result.Success.Should().BeTrue();
        result.Skipped.Should().BeTrue();
        runner.StagesRun.Should().BeEmpty();
    }

    // -------- Baseline-aware test gating --------

    private const string TestFailureOutput =
        """
        [xUnit.net 00:00:01.43]     RepoGPT.Core.Tests.RAG.PersistentVectorIndexTests.SaveAndLoad_PersistsBinaryIndexFilesAcrossPartitions [FAIL]
          Failed RepoGPT.Core.Tests.RAG.PersistentVectorIndexTests.SaveAndLoad_PersistsBinaryIndexFilesAcrossPartitions [48 ms]
          Failed RepoGPT.AspNetCore.Tests.Security.PolicyAuthorizationFilterTests.AllowedPaths_RestrictedPath_ReturnsForbidden [54 ms]
          Failed RepoGPT.AspNetCore.Tests.Security.PolicyAuthorizationFilterTests.AllowedPaths_RestrictedPath_ReturnsForbidden [55 ms]
        Failed!  - Failed:     2, Passed:    48, Skipped:     0, Total:    50, Duration: 1 s - RepoGPT.AspNetCore.Tests.dll (net8.0)
        """;

    [Fact]
    public void ParseFailedTests_ExtractsDistinctNames_IgnoringSummaryLines()
    {
        var failed = ProcessBuildValidator.ParseFailedTests(TestFailureOutput);

        failed.Should().BeEquivalentTo(
            "RepoGPT.Core.Tests.RAG.PersistentVectorIndexTests.SaveAndLoad_PersistsBinaryIndexFilesAcrossPartitions",
            "RepoGPT.AspNetCore.Tests.Security.PolicyAuthorizationFilterTests.AllowedPaths_RestrictedPath_ReturnsForbidden");
    }

    private void AddTestProject() =>
        AddProject("App.Tests.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><PackageReference Include=\"Microsoft.NET.Test.Sdk\" /></ItemGroup></Project>");

    [Fact]
    public async Task PreexistingTestFailures_DoNotBlock_WhenAllInBaseline()
    {
        AddProject();
        AddTestProject();
        var runner = new FakeRunner((_, args) =>
            args.Contains("test") ? new CommandResult(1, TestFailureOutput, false) : new CommandResult(0, "ok", false));

        var validator = Build(runner);
        validator.TestFailureBaseline = ProcessBuildValidator.ParseFailedTests(TestFailureOutput);

        var result = await validator.ValidateAsync();

        result.Success.Should().BeTrue("all failing tests were already failing before the task started");
        result.Stages.Single(s => s.Name == "test").SkipReason.Should().Contain("pre-existing");
    }

    [Fact]
    public async Task NewTestFailure_Blocks_EvenWithBaseline()
    {
        AddProject();
        AddTestProject();
        var withRegression = TestFailureOutput +
            "\n  Failed RepoGPT.Core.Tests.NewFeatureTests.BrandNewRegression [10 ms]";
        var runner = new FakeRunner((_, args) =>
            args.Contains("test") ? new CommandResult(1, withRegression, false) : new CommandResult(0, "ok", false));

        var validator = Build(runner);
        validator.TestFailureBaseline = ProcessBuildValidator.ParseFailedTests(TestFailureOutput);

        var result = await validator.ValidateAsync();

        result.Success.Should().BeFalse();
        result.Summary.Should().Contain("BrandNewRegression").And.Contain("NEW failing test");
    }

    [Fact]
    public async Task TestFailures_Block_WhenNoBaselineCaptured()
    {
        AddProject();
        AddTestProject();
        var runner = new FakeRunner((_, args) =>
            args.Contains("test") ? new CommandResult(1, TestFailureOutput, false) : new CommandResult(0, "ok", false));

        var result = await Build(runner).ValidateAsync();

        result.Success.Should().BeFalse("without a baseline every failing test blocks");
    }

    [Fact]
    public async Task TestFailures_Block_InStrictMode()
    {
        AddProject();
        AddTestProject();
        var runner = new FakeRunner((_, args) =>
            args.Contains("test") ? new CommandResult(1, TestFailureOutput, false) : new CommandResult(0, "ok", false));

        var validator = Build(runner, b => b.FailOnlyOnNewTestFailures = false);
        validator.TestFailureBaseline = ProcessBuildValidator.ParseFailedTests(TestFailureOutput);

        var result = await validator.ValidateAsync();

        result.Success.Should().BeFalse("strict mode blocks on any failing test");
    }

    [Fact]
    public async Task CaptureTestBaseline_RecordsFailingTests()
    {
        AddProject();
        AddTestProject();
        var runner = new FakeRunner((_, args) =>
            args.Contains("test") ? new CommandResult(1, TestFailureOutput, false) : new CommandResult(0, "ok", false));

        var baseline = await Build(runner).CaptureTestBaselineAsync();

        baseline.BuildSucceeded.Should().BeTrue();
        baseline.FailedTests.Should().HaveCount(2);
        baseline.Summary.Should().Contain("pre-existing");
    }

    [Fact]
    public async Task CaptureTestBaseline_ReportsBrokenBuild()
    {
        AddProject();
        AddTestProject();
        var runner = new FakeRunner((_, args) =>
            args.Contains("build") ? new CommandResult(1, "error CS0000", false) : new CommandResult(0, "ok", false));

        var baseline = await Build(runner).CaptureTestBaselineAsync();

        baseline.BuildSucceeded.Should().BeFalse();
        baseline.FailedTests.Should().BeEmpty();
    }

    [Fact]
    public async Task CaptureTestBaseline_IsEmpty_WhenNoTestProjects()
    {
        AddProject();
        var runner = new FakeRunner((_, _) => new CommandResult(0, "ok", false));

        var baseline = await Build(runner).CaptureTestBaselineAsync();

        baseline.FailedTests.Should().BeEmpty();
        runner.StagesRun.Should().BeEmpty("no baseline run is needed without test projects");
    }
}
