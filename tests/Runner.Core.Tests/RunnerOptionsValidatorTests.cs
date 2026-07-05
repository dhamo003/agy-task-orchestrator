using AntigravityTaskRunner.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AntigravityTaskRunner.Core.Tests;

/// <summary>
/// Tests for <see cref="RunnerOptionsValidator"/> ensuring fail-fast on invalid configuration.
/// </summary>
public class RunnerOptionsValidatorTests
{
    private readonly RunnerOptionsValidator _validator = new();

    #region Valid Configuration

    [Fact]
    public void Validate_WithDefaultOptions_ShouldSucceed()
    {
        var options = new RunnerOptions();

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    #endregion

    #region Top-Level Validation

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WithEmptyTasksFile_ShouldFail(string? tasksFile)
    {
        var options = new RunnerOptions { TasksFile = tasksFile! };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("TasksFile");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WithEmptyModel_ShouldFail(string? model)
    {
        var options = new RunnerOptions { Model = model! };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("Model");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WithEmptyWorkspacePath_ShouldFail(string? path)
    {
        var options = new RunnerOptions { WorkspacePath = path! };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("WorkspacePath");
    }

    #endregion

    #region Retry Validation

    [Fact]
    public void Validate_WithNegativeMaxRetries_ShouldFail()
    {
        var options = new RunnerOptions
        {
            Retry = new RetryOptions { MaxRetries = -1 }
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("MaxRetries");
    }

    [Fact]
    public void Validate_WithZeroMaxRetries_ShouldSucceed()
    {
        var options = new RunnerOptions
        {
            Retry = new RetryOptions { MaxRetries = 0 }
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithNonPositiveBackoffBase_ShouldFail(double backoffBase)
    {
        var options = new RunnerOptions
        {
            Retry = new RetryOptions { BackoffBaseSeconds = backoffBase }
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("BackoffBaseSeconds");
    }

    [Fact]
    public void Validate_WithBackoffMaxLessThanBase_ShouldFail()
    {
        var options = new RunnerOptions
        {
            Retry = new RetryOptions
            {
                BackoffBaseSeconds = 10,
                BackoffMaxSeconds = 5
            }
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("BackoffMaxSeconds");
    }

    #endregion

    #region Timeout Validation

    [Fact]
    public void Validate_WithZeroTaskTimeout_ShouldFail()
    {
        var options = new RunnerOptions
        {
            Timeout = new TimeoutOptions { TaskTimeoutMinutes = 0 }
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("TaskTimeoutMinutes");
    }

    [Fact]
    public void Validate_WithSessionTimeoutLessThanTaskTimeout_ShouldFail()
    {
        var options = new RunnerOptions
        {
            Timeout = new TimeoutOptions
            {
                TaskTimeoutMinutes = 60,
                SessionTimeoutMinutes = 30
            }
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("SessionTimeoutMinutes");
    }

    [Fact]
    public void Validate_WithZeroModelSwitchTimeout_ShouldFail()
    {
        var options = new RunnerOptions
        {
            Timeout = new TimeoutOptions { ModelSwitchTimeoutSeconds = 0 }
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("ModelSwitchTimeoutSeconds");
    }

    #endregion

    #region Limits / Build / Checkpoint Validation

    [Fact]
    public void Validate_WithNonPositivePauseSeconds_ShouldFail()
    {
        var options = new RunnerOptions();
        options.Limits.PauseSeconds = 0;

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("PauseSeconds");
    }

    [Fact]
    public void Validate_WithZeroMaxPauses_ShouldFail()
    {
        var options = new RunnerOptions();
        options.Limits.MaxPausesPerTask = 0;

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("MaxPausesPerTask");
    }

    [Fact]
    public void Validate_WithBuildEnabledButNoCommands_ShouldFail()
    {
        var options = new RunnerOptions();
        options.Build.Commands.Clear();

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("Build.Commands");
    }

    [Fact]
    public void Validate_WithBuildDisabled_IgnoresBuildCommands()
    {
        var options = new RunnerOptions();
        options.Build.Enabled = false;
        options.Build.Commands.Clear();

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyBuildCommand_ShouldFail()
    {
        var options = new RunnerOptions();
        options.Build.Commands.Add(new BuildCommandOptions { Name = "custom", Command = "" });

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("custom");
    }

    [Fact]
    public void Validate_WithCheckpointEnabledButNoDirectory_ShouldFail()
    {
        var options = new RunnerOptions();
        options.Checkpoint.Directory = "";

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("Checkpoint.Directory");
    }

    #endregion

    #region Model Validation

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WithEmptyTargetModel_ShouldFail(string? model)
    {
        var options = new RunnerOptions
        {
            ModelConfig = new ModelOptions { TargetModel = model! }
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("TargetModel");
    }

    [Fact]
    public void Validate_WithSwitchCommandMissingPlaceholder_ShouldFail()
    {
        var options = new RunnerOptions
        {
            ModelConfig = new ModelOptions { SwitchCommandTemplate = "--model" }
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("SwitchCommandTemplate");
    }

    #endregion

    #region Prompt Template Validation

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WithEmptyPromptTemplate_ShouldFail(string? template)
    {
        var options = new RunnerOptions
        {
            PromptTemplate = new PromptTemplateOptions { Template = template! }
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("Template");
    }

    #endregion

    #region Completion Validation

    [Fact]
    public void Validate_WithEmptySuccessMarkers_ShouldFail()
    {
        var options = new RunnerOptions
        {
            Completion = new CompletionOptions { SuccessMarkers = [] }
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("SuccessMarkers");
    }

    [Fact]
    public void Validate_WithEmptyFailureMarkers_ShouldFail()
    {
        var options = new RunnerOptions
        {
            Completion = new CompletionOptions { FailureMarkers = [] }
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("FailureMarkers");
    }

    [Fact]
    public void Validate_WithBlankSuccessMarker_ShouldFail()
    {
        var options = new RunnerOptions
        {
            Completion = new CompletionOptions
            {
                SuccessMarkers = ["valid", "", "also valid"]
            }
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("SuccessMarkers");
    }

    #endregion

    #region Multiple Failures

    [Fact]
    public void Validate_WithMultipleInvalidFields_ShouldReportAllFailures()
    {
        var options = new RunnerOptions
        {
            TasksFile = "",
            Model = "",
            WorkspacePath = "",
            Retry = new RetryOptions { MaxRetries = -1 }
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        // Should contain at least TasksFile, Model, WorkspacePath, and MaxRetries errors
        result.FailureMessage.Should().Contain("TasksFile");
        result.FailureMessage.Should().Contain("Model");
        result.FailureMessage.Should().Contain("WorkspacePath");
        result.FailureMessage.Should().Contain("MaxRetries");
    }

    #endregion
}
