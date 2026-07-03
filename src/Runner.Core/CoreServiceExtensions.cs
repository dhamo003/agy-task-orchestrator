using AntigravityTaskRunner.Core.Cancellation;
using AntigravityTaskRunner.Core.Orchestration;
using AntigravityTaskRunner.Core.Pipeline;
using AntigravityTaskRunner.Core.Progress;
using AntigravityTaskRunner.Core.Retry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using AntigravityTaskRunner.Configuration;
using Runner.Markdown.Parser;
using Runner.Logging;
using System;

namespace AntigravityTaskRunner.Core;

public static class CoreServiceExtensions
{
    public static IServiceCollection AddRunnerCore(this IServiceCollection services)
    {
        services.AddSingleton<ICancellationManager, CancellationManager>();
        services.AddSingleton<IProgressTracker, ProgressTracker>();
        services.AddTransient<IRetryPolicy, RetryPolicy>();
        services.AddTransient<AntigravityTaskRunner.Core.Prompts.IPromptTemplateEngine, AntigravityTaskRunner.Core.Prompts.PromptTemplateEngine>();
        services.AddTransient<ITaskPipeline, TaskPipeline>();

        // Register Orchestrator using a factory based on ExecutionMode
        services.AddTransient<ITaskOrchestrator>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RunnerOptions>>().Value;
            var parser = sp.GetRequiredService<ITaskParser>();
            var pipeline = sp.GetRequiredService<ITaskPipeline>();
            var retryPolicy = sp.GetRequiredService<IRetryPolicy>();
            var progressTracker = sp.GetRequiredService<IProgressTracker>();
            var logger = sp.GetRequiredService<ITaskLogger>();
            var optionsSnap = sp.GetRequiredService<IOptions<RunnerOptions>>();
            var writer = sp.GetRequiredService<Runner.Markdown.Writer.ITaskWriter>();

            if (options.Parallel.Mode == ExecutionMode.Parallel)
            {
                return new ParallelOrchestrator(parser, writer, pipeline, retryPolicy, progressTracker, logger, optionsSnap);
            }
            else
            {
                return new SequentialOrchestrator(parser, writer, pipeline, retryPolicy, progressTracker, logger, optionsSnap);
            }
        });

        return services;
    }
}
