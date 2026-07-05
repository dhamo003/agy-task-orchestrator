using AntigravityTaskRunner.Core.Cancellation;
using AntigravityTaskRunner.Core.Checkpointing;
using AntigravityTaskRunner.Core.Orchestration;
using AntigravityTaskRunner.Core.Pipeline;
using AntigravityTaskRunner.Core.Progress;
using AntigravityTaskRunner.Core.Prompts;
using AntigravityTaskRunner.Core.Retry;
using AntigravityTaskRunner.Core.Verification;
using Microsoft.Extensions.DependencyInjection;

namespace AntigravityTaskRunner.Core;

public static class CoreServiceExtensions
{
    public static IServiceCollection AddRunnerCore(this IServiceCollection services)
    {
        services.AddSingleton<ICancellationManager, CancellationManager>();
        services.AddSingleton<IProgressTracker, ProgressTracker>();
        services.AddSingleton<ICheckpointStore, JsonCheckpointStore>();
        services.AddSingleton<ICompletionVerifier, CompletionVerifier>();
        services.AddTransient<IRetryPolicy, RetryPolicy>();
        services.AddTransient<IPromptTemplateEngine, PromptTemplateEngine>();
        services.AddTransient<ITaskPipeline, TaskPipeline>();

        // Execution is strictly sequential by design: exactly one task, one session.
        services.AddTransient<ITaskOrchestrator, SequentialOrchestrator>();

        return services;
    }
}
