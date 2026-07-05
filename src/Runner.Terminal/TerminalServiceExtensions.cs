using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Terminal.Build;
using AntigravityTaskRunner.Terminal.Detection;
using AntigravityTaskRunner.Terminal.Sessions;
using AntigravityTaskRunner.Terminal.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AntigravityTaskRunner.Terminal;

public static class TerminalServiceExtensions
{
    public static IServiceCollection AddTerminalServices(this IServiceCollection services)
    {
        services.AddTransient<ITerminalSession, ProcessTerminalSession>();
        services.AddSingleton<IModelDetector, OutputModelDetector>();
        services.AddSingleton<IModelSwitcher, CliModelSwitcher>();
        services.AddSingleton<ICompletionDetector, MarkerCompletionDetector>();
        services.AddSingleton<ILimitDetector, PatternLimitDetector>();
        services.AddSingleton<SourceFileClassifier>();
        services.AddSingleton<IWorkspaceAnalyzer, FileChangeWorkspaceAnalyzer>();
        services.AddSingleton<IProcessCommandRunner, SystemProcessCommandRunner>();
        services.AddSingleton<IBuildValidator, ProcessBuildValidator>();

        // Session runners: one per execution mode, selected from configuration.
        services.AddTransient<InteractiveAgentRunner>();
        services.AddTransient<OneShotAgentRunner>();
        services.AddTransient<IAgentSessionRunner>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RunnerOptions>>().Value;
            return options.Terminal.ExecutionMode == TerminalExecutionMode.OneShot
                ? sp.GetRequiredService<OneShotAgentRunner>()
                : sp.GetRequiredService<InteractiveAgentRunner>();
        });

        return services;
    }
}
