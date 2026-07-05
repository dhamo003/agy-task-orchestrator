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
                if (!string.IsNullOrWhiteSpace(settings.Workspace))
                {
                    options.WorkspacePath = settings.Workspace;
                }

                // CRITICAL: the workspace analyzer reads the NESTED Runner:Workspace options
                // (options.Workspace.WorkspacePath). It must always agree with the top-level
                // workspace, otherwise change verification watches the wrong directory
                // (e.g. the runner's own publish folder when launched from there).
                options.Workspace.WorkspacePath = !string.IsNullOrWhiteSpace(settings.Workspace)
                    ? settings.Workspace
                    : (options.WorkspacePath == "." ? Environment.CurrentDirectory : options.WorkspacePath);

                if (settings.DryRun)
                {
                    options.DryRun = true;
                }
                
                if (settings.Verbose)
                {
                    options.Verbose = true;
                }

                if (settings.OneShot)
                {
                    options.Terminal = options.Terminal with { ExecutionMode = TerminalExecutionMode.OneShot };
                }

                if (settings.RetryFailed)
                {
                    options.RetryFailedTasks = true;
                }

                if (settings.NoBuildValidation)
                {
                    options.Build.Enabled = false;
                }
            });

            services.PostConfigure<WorkspaceOptions>(options =>
            {
                options.WorkspacePath = !string.IsNullOrWhiteSpace(settings.Workspace) 
                    ? settings.Workspace 
                    : Environment.CurrentDirectory;
            });
            
            if (!string.IsNullOrEmpty(settings.Model))
            {
                services.PostConfigure<ModelOptions>(options =>
                {
                    options.TargetModel = settings.Model;
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
