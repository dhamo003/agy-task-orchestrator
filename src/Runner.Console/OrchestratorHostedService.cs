using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AntigravityTaskRunner.Core.Orchestration;
using AntigravityTaskRunner.Core.Progress;
using Runner.Markdown.Models;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace AntigravityTaskRunner.Console;

public class OrchestratorHostedService : IHostedService
{
    private readonly ITaskOrchestrator _orchestrator;
    private readonly IProgressTracker _progressTracker;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly IAnsiConsole _console;
    
    public OrchestratorHostedService(
        ITaskOrchestrator orchestrator,
        IProgressTracker progressTracker,
        IHostApplicationLifetime appLifetime,
        IAnsiConsole console)
    {
        _orchestrator = orchestrator;
        _progressTracker = progressTracker;
        _appLifetime = appLifetime;
        _console = console;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _appLifetime.ApplicationStarted.Register(() =>
        {
            Task.Run(async () => await ExecuteAsync(cancellationToken));
        });
        
        await Task.CompletedTask;
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Build stamp: makes it immediately obvious when a stale publish is running.
            var entryAssembly = System.Reflection.Assembly.GetEntryAssembly()?.Location;
            if (!string.IsNullOrEmpty(entryAssembly) && System.IO.File.Exists(entryAssembly))
            {
                var stamp = System.IO.File.GetLastWriteTime(entryAssembly)
                    .ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                _console.MarkupLine($"[grey]AntigravityTaskRunner binary built {stamp}[/]");
            }

            var allTasks = new System.Collections.Generic.List<(TaskItem Task, bool Success, TimeSpan Duration)>();

            await _console.Progress()
                .AutoRefresh(true)
                .AutoClear(false)
                .HideCompleted(false)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new ElapsedTimeColumn(),
                    new SpinnerColumn()
                )
                .StartAsync(async ctx =>
                {
                    var mainTask = ctx.AddTask("[green]Orchestrating tasks...[/]");
                    mainTask.IsIndeterminate = true;

                    AntigravityTaskRunner.Core.Models.PipelineHaltReport? haltReport = null;

                    void OnTaskStarted(object? sender, TaskItem task)
                    {
                        mainTask.Description = $"[blue]Running:[/] {Markup.Escape(task.DisplayText)}";
                    }

                    // Live per-phase status (AI processing heartbeats, verification, build,
                    // tests, pauses) so the UI never appears frozen.
                    void OnStatusChanged(object? sender, (TaskItem Task, string Status) args)
                    {
                        mainTask.Description =
                            $"[blue]{Markup.Escape(args.Task.DisplayText)}[/] — [grey]{Markup.Escape(args.Status)}[/]";
                    }

                    void OnTaskCompleted(object? sender, (TaskItem Task, bool Success, TimeSpan Duration) args)
                    {
                        allTasks.Add(args);
                        var statusStr = args.Success ? "[green]Completed[/]" : "[red]Failed[/]";
                        _console.MarkupLine($"Task '{Markup.Escape(args.Task.DisplayText)}' {statusStr} in {args.Duration.TotalSeconds:F1}s");
                    }

                    void OnPipelineHalted(object? sender, AntigravityTaskRunner.Core.Models.PipelineHaltReport report)
                    {
                        haltReport = report;
                    }

                    _progressTracker.TaskStarted += OnTaskStarted;
                    _progressTracker.StatusChanged += OnStatusChanged;
                    _progressTracker.TaskCompleted += OnTaskCompleted;
                    _progressTracker.PipelineHalted += OnPipelineHalted;

                    try
                    {
                        await _orchestrator.RunAllAsync(cancellationToken);
                        mainTask.StopTask();
                        if (haltReport is not null)
                        {
                            mainTask.Description = "[red]Pipeline halted on failure.[/]";
                            Environment.ExitCode = 2;
                        }
                        else
                        {
                            mainTask.Description = "[green]All tasks finished.[/]";
                            Environment.ExitCode = _progressTracker.TasksFailed > 0 ? 1 : 0;
                        }
                    }
                    finally
                    {
                        _progressTracker.TaskStarted -= OnTaskStarted;
                        _progressTracker.StatusChanged -= OnStatusChanged;
                        _progressTracker.TaskCompleted -= OnTaskCompleted;
                        _progressTracker.PipelineHalted -= OnPipelineHalted;
                    }

                    if (haltReport is not null)
                    {
                        _console.WriteLine();
                        var panel = new Panel(Markup.Escape(haltReport.Describe()))
                            .Header("[red]Pipeline halted — task NOT skipped[/]")
                            .Border(BoxBorder.Heavy)
                            .BorderColor(Color.Red);
                        _console.Write(panel);
                    }
                });

            // Summary Reporting
            _console.WriteLine();
            var summaryTable = new Table().Border(TableBorder.Rounded).Title("[yellow]Execution Summary[/]");
            summaryTable.AddColumn("Total Processed");
            summaryTable.AddColumn("Succeeded");
            summaryTable.AddColumn("Failed");
            summaryTable.AddRow(
                _progressTracker.TasksProcessed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                $"[green]{_progressTracker.TasksSucceeded}[/]",
                _progressTracker.TasksFailed > 0 ? $"[red]{_progressTracker.TasksFailed}[/]" : "0"
            );
            _console.Write(summaryTable);

            var failedTasks = allTasks.Where(t => !t.Success).ToList();
            if (failedTasks.Count > 0)
            {
                _console.WriteLine();
                var errorTable = new Table().Border(TableBorder.Rounded).Title("[red]Failed Tasks[/]");
                errorTable.AddColumn("Task Name");
                errorTable.AddColumn("Duration");
                
                foreach(var t in failedTasks)
                {
                    errorTable.AddRow($"[red]{Markup.Escape(t.Task.DisplayText)}[/]", $"{t.Duration.TotalSeconds:F1}s");
                }
                _console.Write(errorTable);
            }

            _console.WriteLine();
            _console.MarkupLine("[cyan]Logs have been saved to the configured log directory.[/]");

        }
        catch (TaskCanceledException)
        {
            _console.MarkupLine("[yellow]Execution was cancelled.[/]");
            Environment.ExitCode = 130;
        }
        catch (Exception ex)
        {
            _console.WriteException(ex);
            Environment.ExitCode = 2;
        }
        finally
        {
            _appLifetime.StopApplication();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _orchestrator.StopAsync();
    }
}
