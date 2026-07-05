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
        ValidateModel(options.ModelConfig, failures);
        ValidatePromptTemplate(options.PromptTemplate, failures);
        ValidateCompletion(options.Completion, failures);
        ValidateTerminal(options.Terminal, failures);
        ValidateLimits(options.Limits, failures);
        ValidateBuild(options.Build, failures);
        ValidateCheckpoint(options.Checkpoint, failures);

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

        if (timeout.SessionTeardownSeconds <= 0)
        {
            failures.Add("Timeout.SessionTeardownSeconds must be positive.");
        }

        if (timeout.SessionTimeoutMinutes < timeout.TaskTimeoutMinutes)
        {
            failures.Add("Timeout.SessionTimeoutMinutes must be >= TaskTimeoutMinutes.");
        }
    }

    private static void ValidateLimits(LimitOptions limits, List<string> failures)
    {
        if (limits.PauseSeconds <= 0)
        {
            failures.Add("Limits.PauseSeconds must be positive.");
        }

        if (limits.MaxPausesPerTask < 1)
        {
            failures.Add("Limits.MaxPausesPerTask must be at least 1.");
        }
    }

    private static void ValidateBuild(BuildValidationOptions build, List<string> failures)
    {
        if (!build.Enabled)
        {
            return;
        }

        if (build.Commands.Count == 0)
        {
            failures.Add("Build.Commands must contain at least one command when Build.Enabled is true.");
        }

        foreach (var command in build.Commands)
        {
            if (string.IsNullOrWhiteSpace(command.Command))
            {
                failures.Add($"Build command '{command.Name}' must have a non-empty Command.");
            }

            if (command.TimeoutMinutes <= 0)
            {
                failures.Add($"Build command '{command.Name}' must have a positive TimeoutMinutes.");
            }
        }
    }

    private static void ValidateCheckpoint(CheckpointOptions checkpoint, List<string> failures)
    {
        if (!checkpoint.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(checkpoint.Directory))
        {
            failures.Add("Checkpoint.Directory must not be empty when checkpointing is enabled.");
        }

        if (string.IsNullOrWhiteSpace(checkpoint.CheckpointFileName))
        {
            failures.Add("Checkpoint.CheckpointFileName must not be empty when checkpointing is enabled.");
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

        if (terminal.ExecutionMode == TerminalExecutionMode.OneShot)
        {
            if (string.IsNullOrWhiteSpace(terminal.AgentCommand))
            {
                failures.Add("Terminal.AgentCommand must not be empty when ExecutionMode is OneShot.");
            }

            if (terminal.OneShotArguments.Count == 0)
            {
                failures.Add("Terminal.OneShotArguments must contain at least one argument when ExecutionMode is OneShot.");
            }
            else if (!terminal.OneShotArguments.Any(a => a.Contains("{prompt}", StringComparison.Ordinal)))
            {
                failures.Add("Terminal.OneShotArguments must include the '{prompt}' placeholder when ExecutionMode is OneShot.");
            }
        }
    }
}
