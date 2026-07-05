using System.ComponentModel;
using Spectre.Console.Cli;

namespace AntigravityTaskRunner.Console.Commands;

public sealed class RunSettings : CommandSettings
{
    [CommandOption("-w|--workspace <PATH>")]
    [Description("The workspace root directory path. Defaults to current directory.")]
    public string? Workspace { get; init; }

    [CommandOption("-t|--tasks <FILE>")]
    [Description("The markdown file containing tasks. Defaults to tasks.md.")]
    [DefaultValue("tasks.md")]
    public string TasksFile { get; init; } = "tasks.md";

    [CommandOption("-m|--model <MODEL>")]
    [Description("The target model to use for completion.")]
    public string? Model { get; init; }

    [CommandOption("--dry-run")]
    [Description("Parse and display tasks without executing them.")]
    [DefaultValue(false)]
    public bool DryRun { get; init; }

    [CommandOption("-v|--verbose")]
    [Description("Enable detailed terminal output passthrough.")]
    [DefaultValue(false)]
    public bool Verbose { get; init; }

    [CommandOption("--one-shot")]
    [Description("Run each task by launching 'agy' in one-shot (print) mode and detecting completion from the real process exit, instead of driving an interactive session. Overrides Terminal.ExecutionMode.")]
    [DefaultValue(false)]
    public bool OneShot { get; init; }

    [CommandOption("--retry-failed")]
    [Description("Re-attempt a task previously marked failed [!] instead of halting on it. Tasks are never skipped either way.")]
    [DefaultValue(false)]
    public bool RetryFailed { get; init; }

    [CommandOption("--no-build-validation")]
    [Description("Disable the dotnet restore/build/test validation stage (not recommended for unattended runs).")]
    [DefaultValue(false)]
    public bool NoBuildValidation { get; init; }
}
