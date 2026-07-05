using System.Diagnostics;
using System.Text.RegularExpressions;
using AntigravityTaskRunner.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AntigravityTaskRunner.Terminal.Build;

/// <summary>
/// Runs the configured validation stages (default: dotnet restore → build → test)
/// in the workspace root. Stops at the first failing stage. Detecting no buildable
/// project can either skip validation or fail it, per configuration.
/// </summary>
public sealed class ProcessBuildValidator : IBuildValidator
{
    private static readonly EnumerationOptions Enumeration = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        MaxRecursionDepth = 8,
    };

    /// <summary>
    /// Matches the per-test failure lines of the `dotnet test` console logger, e.g.
    /// "  Failed RepoGPT.Core.Tests.RAG.PersistentVectorIndexTests.SaveAndLoad_… [48 ms]".
    /// </summary>
    private static readonly Regex FailedTestLine = new(
        @"^\s*Failed\s+(?<name>[A-Za-z_][\w.+`<>\[\]]*)\s*(?:\[[\d.,]+\s*m?s\])?\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private readonly BuildValidationOptions _options;
    private readonly string _workspacePath;
    private readonly IProcessCommandRunner _runner;
    private readonly ILogger<ProcessBuildValidator> _logger;

    public ProcessBuildValidator(
        IOptions<RunnerOptions> options,
        IProcessCommandRunner runner,
        ILogger<ProcessBuildValidator> logger)
    {
        _options = options.Value.Build;
        _workspacePath = string.IsNullOrWhiteSpace(options.Value.WorkspacePath) || options.Value.WorkspacePath == "."
            ? Environment.CurrentDirectory
            : options.Value.WorkspacePath;
        _runner = runner;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlySet<string>? TestFailureBaseline { get; set; }

    public async Task<TestBaseline> CaptureTestBaselineAsync(CancellationToken token = default)
    {
        if (!_options.Enabled || !_options.RunTests || !HasBuildableProject() || !HasTestProjects())
        {
            return TestBaseline.Empty;
        }

        _logger.LogInformation("Capturing pre-task test baseline (which tests already fail)...");

        foreach (var command in _options.Commands)
        {
            token.ThrowIfCancellationRequested();

            var stage = await RunStageAsync(command, token);
            if (IsTestStage(command))
            {
                var failed = ParseFailedTests(stage.Output);
                var summary = failed.Count == 0
                    ? "Baseline: all tests pass."
                    : $"Baseline: {failed.Count} pre-existing failing test(s): {string.Join(", ", failed.Take(10))}";
                _logger.LogInformation("{Summary}", summary);
                return new TestBaseline(true, failed, summary);
            }

            if (!stage.Success)
            {
                // The workspace does not even build before any agent work: no test
                // baseline can exist. Tasks will (correctly) halt on the build stage.
                return new TestBaseline(false, new HashSet<string>(StringComparer.Ordinal),
                    $"Baseline capture: stage '{stage.Name}' failed before any task ran — fix the workspace build first.");
            }
        }

        return TestBaseline.Empty;
    }

    public async Task<BuildValidationResult> ValidateAsync(CancellationToken token = default)
    {
        if (!_options.Enabled)
        {
            return BuildValidationResult.SkippedResult("Build validation disabled by configuration.");
        }

        if (!HasBuildableProject())
        {
            if (_options.SkipWhenNoProject)
            {
                return BuildValidationResult.SkippedResult(
                    "No .sln/.slnx/.csproj found in workspace; build validation skipped.");
            }

            return new BuildValidationResult(false, false, [],
                "No buildable project found in workspace and SkipWhenNoProject is false.");
        }

        var stages = new List<BuildStageResult>();
        var hasTests = HasTestProjects();

        foreach (var command in _options.Commands)
        {
            token.ThrowIfCancellationRequested();

            if (IsTestStage(command) && (!_options.RunTests || !hasTests))
            {
                stages.Add(new BuildStageResult(command.Name, Describe(command), Success: true, Skipped: true,
                    ExitCode: 0, Output: string.Empty, Duration: TimeSpan.Zero,
                    SkipReason: _options.RunTests ? "No test projects found." : "Tests disabled by configuration."));
                continue;
            }

            var stage = await RunStageAsync(command, token);

            // Baseline-aware test gating: pre-existing failures don't block, regressions do.
            if (!stage.Success && IsTestStage(command) &&
                _options.FailOnlyOnNewTestFailures && TestFailureBaseline is { } baseline)
            {
                var failedNow = ParseFailedTests(stage.Output);
                var newFailures = failedNow.Where(t => !baseline.Contains(t)).ToList();

                if (failedNow.Count > 0 && newFailures.Count == 0)
                {
                    _logger.LogWarning(
                        "Test stage: {Count} failing test(s), all pre-existing (in baseline). Not blocking the task.",
                        failedNow.Count);
                    stage = stage with
                    {
                        Success = true,
                        SkipReason = $"{failedNow.Count} failing test(s) are pre-existing (present in the pre-task baseline); no regressions introduced.",
                    };
                }
                else if (newFailures.Count > 0)
                {
                    stages.Add(stage);
                    return new BuildValidationResult(false, false, stages,
                        $"Stage 'test' failed: {newFailures.Count} NEW failing test(s) (regressions): {string.Join(", ", newFailures.Take(10))}");
                }
            }

            stages.Add(stage);

            if (!stage.Success)
            {
                var failSummary = stage.Output.Length > 0
                    ? $"Stage '{stage.Name}' failed (exit {stage.ExitCode})."
                    : $"Stage '{stage.Name}' failed.";
                return new BuildValidationResult(false, false, stages, failSummary);
            }
        }

        return new BuildValidationResult(true, false, stages, "All build/test validation stages passed.");
    }

    /// <summary>Extracts the distinct failed-test names from `dotnet test` console output.</summary>
    public static HashSet<string> ParseFailedTests(string output)
    {
        var failed = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in FailedTestLine.Matches(output))
        {
            var name = match.Groups["name"].Value;
            // Guard against the summary line "Failed! - Failed: 2, ..." and bare words.
            if (name.Contains('.') && !name.EndsWith('.'))
            {
                failed.Add(name);
            }
        }

        return failed;
    }

    private async Task<BuildStageResult> RunStageAsync(BuildCommandOptions command, CancellationToken token)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Build validation stage '{Stage}': {Command} {Args}",
            command.Name, command.Command, string.Join(' ', command.Arguments));

        CommandResult result;
        try
        {
            result = await _runner.RunAsync(
                command.Command, command.Arguments, _workspacePath,
                TimeSpan.FromMinutes(command.TimeoutMinutes), token);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new BuildStageResult(command.Name, Describe(command), Success: false, Skipped: false,
                ExitCode: -1, Output: $"Failed to start command: {ex.Message}", stopwatch.Elapsed);
        }

        stopwatch.Stop();

        var output = Truncate(result.Output, _options.MaxCapturedOutputChars);
        if (result.TimedOut)
        {
            return new BuildStageResult(command.Name, Describe(command), Success: false, Skipped: false,
                ExitCode: result.ExitCode,
                Output: output + Environment.NewLine + $"[stage timed out after {command.TimeoutMinutes} minute(s)]",
                stopwatch.Elapsed);
        }

        return new BuildStageResult(command.Name, Describe(command),
            Success: result.ExitCode == 0, Skipped: false,
            ExitCode: result.ExitCode, Output: output, Duration: stopwatch.Elapsed);
    }

    private bool HasBuildableProject()
    {
        try
        {
            return EnumerateWorkspaceFiles("*.sln").Any()
                || EnumerateWorkspaceFiles("*.slnx").Any()
                || EnumerateWorkspaceFiles("*.csproj").Any();
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    private bool HasTestProjects() =>
        EnumerateWorkspaceFiles("*.csproj").Any(p =>
        {
            var name = Path.GetFileNameWithoutExtension(p);
            if (name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".Test", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                return File.ReadAllText(p).Contains("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase);
            }
            catch (IOException)
            {
                return false;
            }
        });

    private IEnumerable<string> EnumerateWorkspaceFiles(string pattern)
    {
        if (!Directory.Exists(_workspacePath))
        {
            return [];
        }

        return Directory.EnumerateFiles(_workspacePath, pattern, Enumeration)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"));
    }

    private static bool IsTestStage(BuildCommandOptions command) =>
        string.Equals(command.Name, "test", StringComparison.OrdinalIgnoreCase);

    private static string Describe(BuildCommandOptions command) =>
        $"{command.Command} {string.Join(' ', command.Arguments)}";

    private static string Truncate(string value, int maxChars) =>
        value.Length <= maxChars ? value : "…" + value[^maxChars..];
}
