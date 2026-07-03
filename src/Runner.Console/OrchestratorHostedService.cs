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

                    void OnTaskStarted(object? sender, TaskItem task)
                    {
                        mainTask.Description = $"[blue]Running:[/] {Markup.Escape(task.DisplayText)}";
                    }

                    void OnTaskCompleted(object? sender, (TaskItem Task, bool Success, TimeSpan Duration) args)
                    {
                        allTasks.Add(args);
                        var statusStr = args.Success ? "[green]Completed[/]" : "[red]Failed[/]";
                        _console.MarkupLine($"Task '{Markup.Escape(args.Task.DisplayText)}' {statusStr} in {args.Duration.TotalSeconds:F1}s");
                    }

                    _progressTracker.TaskStarted += OnTaskStarted;
                    _progressTracker.TaskCompleted += OnTaskCompleted;

                    try
                    {
                        await _orchestrator.RunAllAsync(cancellationToken);
                        mainTask.StopTask();
                        mainTask.Description = "[green]All tasks finished.[/]";
                        Environment.ExitCode = _progressTracker.TasksFailed > 0 ? 1 : 0;
                    }
                    finally
                    {
                        _progressTracker.TaskStarted -= OnTaskStarted;
                        _progressTracker.TaskCompleted -= OnTaskCompleted;
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
