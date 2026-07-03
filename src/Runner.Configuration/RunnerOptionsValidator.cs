using Microsoft.Extensions.Options;

namespace AntigravityTaskRunner.Configuration;

/// <summary>
/// Validates <see cref="RunnerOptions"/> at startup for fail-fast behavior.
/// Registered via <see cref="ConfigurationServiceExtensions"/>.
/// </summary>
public sealed class RunnerOptionsValidator : IValidateOptions<RunnerOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, RunnerOptions options)
    {
        var failures = new List<string>();

        ValidateTopLevel(options, failures);
        ValidateRetry(options.Retry, failures);
        ValidateTimeout(options.Timeout, failures);
        ValidateParallel(options.Parallel, failures);
        ValidateModel(options.ModelConfig, failures);
        ValidatePromptTemplate(options.PromptTemplate, failures);
        ValidateCompletion(options.Completion, failures);
        ValidateTerminal(options.Terminal, failures);

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateTopLevel(RunnerOptions options, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(options.TasksFile))
        {
            failures.Add("TasksFile must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            failures.Add("Model must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(options.WorkspacePath))
        {
            failures.Add("WorkspacePath must not be empty.");
        }
    }

    private static void ValidateRetry(RetryOptions retry, List<string> failures)
    {
        if (retry.MaxRetries < 0)
        {
            failures.Add("Retry.MaxRetries must be non-negative.");
        }

        if (retry.BackoffBaseSeconds <= 0)
        {
            failures.Add("Retry.BackoffBaseSeconds must be positive.");
        }

        if (retry.BackoffMaxSeconds <= 0)
        {
            failures.Add("Retry.BackoffMaxSeconds must be positive.");
        }

        if (retry.BackoffMaxSeconds < retry.BackoffBaseSeconds)
        {
            failures.Add("Retry.BackoffMaxSeconds must be >= BackoffBaseSeconds.");
        }
    }

    private static void ValidateTimeout(TimeoutOptions timeout, List<string> failures)
    {
        if (timeout.TaskTimeoutMinutes <= 0)
        {
            failures.Add("Timeout.TaskTimeoutMinutes must be positive.");
        }

        if (timeout.SessionTimeoutMinutes <= 0)
        {
            failures.Add("Timeout.SessionTimeoutMinutes must be positive.");
        }

        if (timeout.ModelSwitchTimeoutSeconds <= 0)
        {
            failures.Add("Timeout.ModelSwitchTimeoutSeconds must be positive.");
        }

        if (timeout.SessionTimeoutMinutes < timeout.TaskTimeoutMinutes)
        {
            failures.Add("Timeout.SessionTimeoutMinutes must be >= TaskTimeoutMinutes.");
        }
    }

    private static void ValidateParallel(ParallelExecutionOptions parallel, List<string> failures)
    {
        if (parallel.MaxWorkers < 1)
        {
            failures.Add("Parallel.MaxWorkers must be at least 1.");
        }

        if (parallel.Mode == ExecutionMode.Parallel && parallel.MaxWorkers < 2)
        {
            failures.Add("Parallel.MaxWorkers must be at least 2 when Mode is Parallel.");
        }
    }

    private static void ValidateModel(ModelOptions model, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(model.TargetModel))
        {
            failures.Add("ModelConfig.TargetModel must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(model.SwitchCommandTemplate))
        {
            failures.Add("ModelConfig.SwitchCommandTemplate must not be empty.");
        }
        else if (!model.SwitchCommandTemplate.Contains("{model}", StringComparison.Ordinal))
        {
            failures.Add("ModelConfig.SwitchCommandTemplate must contain the '{model}' placeholder.");
        }
    }

    private static void ValidatePromptTemplate(PromptTemplateOptions prompt, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(prompt.Template))
        {
            failures.Add("PromptTemplate.Template must not be empty.");
        }
    }

    private static void ValidateCompletion(CompletionOptions completion, List<string> failures)
    {
        if (completion.SuccessMarkers.Count == 0)
        {
            failures.Add("Completion.SuccessMarkers must contain at least one marker.");
        }

        if (completion.FailureMarkers.Count == 0)
        {
            failures.Add("Completion.FailureMarkers must contain at least one marker.");
        }

        if (completion.SuccessMarkers.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add("Completion.SuccessMarkers must not contain empty strings.");
        }

        if (completion.FailureMarkers.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add("Completion.FailureMarkers must not contain empty strings.");
        }
    }

    private static void ValidateTerminal(TerminalOptions terminal, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(terminal.ShellPath))
        {
            failures.Add("Terminal.ShellPath must not be empty.");
        }
    }
}
