using System;
using System.Threading.Tasks;
using AntigravityTaskRunner.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AntigravityTaskRunner.Console.Commands;

public sealed class RunCommand : AsyncCommand<RunSettings>
{
    private readonly IHostBuilder _hostBuilder;

    public RunCommand(IHostBuilder hostBuilder)
    {
        _hostBuilder = hostBuilder ?? throw new ArgumentNullException(nameof(hostBuilder));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, RunSettings settings)
    {
        // Configure options based on settings
        _hostBuilder.ConfigureServices((ctx, services) =>
        {
            services.PostConfigure<RunnerOptions>(options =>
            {
                options.TasksFile = settings.TasksFile;

                if (settings.DryRun)
                {
                    options.DryRun = true;
                }
                
                if (settings.Verbose)
                {
                    options.Verbose = true;
                }
            });

            services.PostConfigure<WorkspaceOptions>(options =>
            {
                options.WorkspacePath = Environment.CurrentDirectory;
                // Currently TasksFile is part of MarkdownEngine options or WorkspaceOptions?
                // Depending on Phase B implementation. We assume the path is resolved via ITaskParser initialization.
            });
            
            if (!string.IsNullOrEmpty(settings.Model))
            {
                services.PostConfigure<ModelOptions>(options =>
                {
                    options.TargetModel = settings.Model;
                });
            }
            
            if (settings.ParallelCount.HasValue)
            {
                services.PostConfigure<ParallelExecutionOptions>(options =>
                {
                    options.MaxWorkers = settings.ParallelCount.Value;
                    options.Mode = ExecutionMode.Parallel;
                });
            }
        });

        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine("[yellow]Dry-run mode enabled. Tasks will be parsed but not executed.[/]");
        }

        var host = _hostBuilder.Build();
        
        try
        {
            await host.RunAsync();
            return Environment.ExitCode == 0 ? 0 : Environment.ExitCode;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 2; // Error
        }
    }
}
