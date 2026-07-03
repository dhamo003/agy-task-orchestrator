using AntigravityTaskRunner.Terminal.Detection;
using AntigravityTaskRunner.Terminal.Sessions;
using AntigravityTaskRunner.Terminal.Workspace;
using Microsoft.Extensions.DependencyInjection;

namespace AntigravityTaskRunner.Terminal;

public static class TerminalServiceExtensions
{
    public static IServiceCollection AddTerminalServices(this IServiceCollection services)
    {
        services.AddTransient<ITerminalSession, ProcessTerminalSession>();
        services.AddSingleton<IModelDetector, OutputModelDetector>();
        services.AddSingleton<IModelSwitcher, CliModelSwitcher>();
        services.AddSingleton<ICompletionDetector, MarkerCompletionDetector>();
        services.AddSingleton<IWorkspaceAnalyzer, FileChangeWorkspaceAnalyzer>();

        return services;
    }
}
