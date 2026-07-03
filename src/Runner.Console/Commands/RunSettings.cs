using System.ComponentModel;
using Spectre.Console.Cli;

namespace AntigravityTaskRunner.Console.Commands;

public sealed class RunSettings : CommandSettings
{
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

    [CommandOption("-p|--parallel <COUNT>")]
    [Description("Number of parallel workers to use.")]
    public int? ParallelCount { get; init; }

    [CommandOption("-v|--verbose")]
    [Description("Enable detailed terminal output passthrough.")]
    [DefaultValue(false)]
    public bool Verbose { get; init; }
}
