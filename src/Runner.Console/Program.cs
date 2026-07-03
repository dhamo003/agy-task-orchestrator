using System;
using AntigravityTaskRunner.Console.Commands;
using AntigravityTaskRunner.Console.Infrastructure;
using AntigravityTaskRunner.Console;
using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Core;
using Runner.Logging;
using Runner.Markdown;
using AntigravityTaskRunner.Terminal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console.Cli;

var hostBuilder = Host.CreateDefaultBuilder(args)
    .UseContentRoot(AppContext.BaseDirectory)
    .ConfigureServices((context, services) =>
    {
        services.AddRunnerConfiguration(context.Configuration);
        services.AddTaskLogging("logs");
        services.AddMarkdownEngine();
        services.AddTerminalServices();
        services.AddRunnerCore();
        
        services.AddSingleton(Spectre.Console.AnsiConsole.Console);
        services.AddHostedService<OrchestratorHostedService>();
    });

var registrations = new ServiceCollection();
registrations.AddSingleton(hostBuilder);

var registrar = new TypeRegistrar(registrations);
var app = new CommandApp<RunCommand>(registrar);

app.Configure(config =>
{
    config.SetApplicationName("antigravity");
    config.AddExample(["--model", "gpt-4", "--parallel", "4"]);
});

return app.Run(args);
