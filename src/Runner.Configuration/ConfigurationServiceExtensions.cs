using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AntigravityTaskRunner.Configuration;

/// <summary>
/// Extension methods for registering configuration services in the DI container.
/// </summary>
public static class ConfigurationServiceExtensions
{
    /// <summary>
    /// Registers all Runner configuration options from the specified <paramref name="configuration"/>
    /// and wires up validation for fail-fast behavior on startup.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The configuration root containing the "Runner" section.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRunnerConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(RunnerOptions.SectionName);

        services.Configure<RunnerOptions>(section);
        services.Configure<RetryOptions>(section.GetSection(nameof(RunnerOptions.Retry)));
        services.Configure<TimeoutOptions>(section.GetSection(nameof(RunnerOptions.Timeout)));
        services.Configure<ParallelExecutionOptions>(section.GetSection(nameof(RunnerOptions.Parallel)));
        services.Configure<ModelOptions>(section.GetSection(nameof(RunnerOptions.ModelConfig)));
        services.Configure<WorkspaceOptions>(section.GetSection(nameof(RunnerOptions.Workspace)));
        services.Configure<PromptTemplateOptions>(section.GetSection(nameof(RunnerOptions.PromptTemplate)));
        services.Configure<CompletionOptions>(section.GetSection(nameof(RunnerOptions.Completion)));

        // Register validator for fail-fast on bad configuration
        services.AddSingleton<IValidateOptions<RunnerOptions>, RunnerOptionsValidator>();

        return services;
    }
}
