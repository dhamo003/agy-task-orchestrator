namespace AntigravityTaskRunner.Configuration;

/// <summary>
/// A single build/validation command executed in the workspace after an attempt.
/// </summary>
public sealed record BuildCommandOptions
{
    /// <summary>Executable to run (e.g. "dotnet").</summary>
    public string Command { get; init; } = "dotnet";

    /// <summary>Arguments passed to the executable.</summary>
    public List<string> Arguments { get; init; } = [];

    /// <summary>Human-readable stage name used in logs and reports (e.g. "restore").</summary>
    public string Name { get; init; } = "build";

    /// <summary>Timeout in minutes for this command.</summary>
    public int TimeoutMinutes { get; init; } = 10;
}

/// <summary>
/// Configuration for post-attempt build &amp; test validation.
/// Binds to the "Runner:Build" section in appsettings.json.
/// </summary>
public sealed class BuildValidationOptions
{
    /// <summary>Master switch. When false no build validation runs.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When true and no .sln/.slnx/.csproj is found in the workspace, validation is
    /// skipped (reported as skipped, not failed) instead of failing the task.
    /// </summary>
    public bool SkipWhenNoProject { get; set; } = true;

    /// <summary>
    /// When true, the test stage runs when test projects are present.
    /// </summary>
    public bool RunTests { get; set; } = true;

    /// <summary>
    /// When true (default), a baseline of already-failing tests is captured BEFORE the
    /// first task runs, and the test stage only fails a task when NEW tests fail
    /// (regressions). Pre-existing failures are reported but do not block the pipeline —
    /// otherwise a repository with any red test could never process any task.
    /// Set to false for strict mode: every failing test blocks.
    /// </summary>
    public bool FailOnlyOnNewTestFailures { get; set; } = true;

    /// <summary>
    /// Ordered validation stages. Defaults to dotnet restore → build → test.
    /// A stage named "test" is only executed when <see cref="RunTests"/> is true
    /// and test projects exist in the workspace.
    /// </summary>
    public List<BuildCommandOptions> Commands { get; set; } =
    [
        new() { Name = "restore", Command = "dotnet", Arguments = ["restore"], TimeoutMinutes = 10 },
        new() { Name = "build", Command = "dotnet", Arguments = ["build", "--no-restore"], TimeoutMinutes = 15 },
        new() { Name = "test", Command = "dotnet", Arguments = ["test", "--no-build"], TimeoutMinutes = 30 },
    ];

    /// <summary>Maximum characters of command output preserved per stage in reports.</summary>
    public int MaxCapturedOutputChars { get; set; } = 20_000;
}
