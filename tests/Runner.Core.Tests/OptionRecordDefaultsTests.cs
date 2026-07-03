using AntigravityTaskRunner.Configuration;
using FluentAssertions;
using Xunit;

namespace AntigravityTaskRunner.Core.Tests;

/// <summary>
/// Tests for option record default values and computed properties.
/// </summary>
public class OptionRecordDefaultsTests
{
    #region RunnerOptions Defaults

    [Fact]
    public void RunnerOptions_Defaults_ShouldHaveSensibleValues()
    {
        var options = new RunnerOptions();

        options.TasksFile.Should().Be("tasks.md");
        options.Model.Should().Be("gemini-3.5-flash-high");
        options.DryRun.Should().BeFalse();
        options.Verbose.Should().BeFalse();
        options.WorkspacePath.Should().Be(".");
        options.Retry.Should().NotBeNull();
        options.Timeout.Should().NotBeNull();
        options.Parallel.Should().NotBeNull();
        options.ModelConfig.Should().NotBeNull();
        options.Workspace.Should().NotBeNull();
        options.PromptTemplate.Should().NotBeNull();
        options.Completion.Should().NotBeNull();
    }

    [Fact]
    public void RunnerOptions_SectionName_ShouldBeRunner()
    {
        RunnerOptions.SectionName.Should().Be("Runner");
    }

    #endregion

    #region RetryOptions Defaults

    [Fact]
    public void RetryOptions_Defaults_ShouldHaveSensibleValues()
    {
        var options = new RetryOptions();

        options.MaxRetries.Should().Be(3);
        options.BackoffBaseSeconds.Should().Be(5);
        options.BackoffMaxSeconds.Should().Be(300);
        options.UseJitter.Should().BeTrue();
    }

    #endregion

    #region TimeoutOptions Defaults and Computed Properties

    [Fact]
    public void TimeoutOptions_Defaults_ShouldHaveSensibleValues()
    {
        var options = new TimeoutOptions();

        options.TaskTimeoutMinutes.Should().Be(30);
        options.SessionTimeoutMinutes.Should().Be(60);
        options.ModelSwitchTimeoutSeconds.Should().Be(30);
    }

    [Fact]
    public void TimeoutOptions_TaskTimeout_ShouldReturnCorrectTimeSpan()
    {
        var options = new TimeoutOptions { TaskTimeoutMinutes = 15 };

        options.TaskTimeout.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void TimeoutOptions_SessionTimeout_ShouldReturnCorrectTimeSpan()
    {
        var options = new TimeoutOptions { SessionTimeoutMinutes = 120 };

        options.SessionTimeout.Should().Be(TimeSpan.FromMinutes(120));
    }

    [Fact]
    public void TimeoutOptions_ModelSwitchTimeout_ShouldReturnCorrectTimeSpan()
    {
        var options = new TimeoutOptions { ModelSwitchTimeoutSeconds = 45 };

        options.ModelSwitchTimeout.Should().Be(TimeSpan.FromSeconds(45));
    }

    #endregion

    #region ParallelExecutionOptions Defaults

    [Fact]
    public void ParallelExecutionOptions_Defaults_ShouldBeSequentialWithOneWorker()
    {
        var options = new ParallelExecutionOptions();

        options.MaxWorkers.Should().Be(1);
        options.Mode.Should().Be(ExecutionMode.Sequential);
    }

    #endregion

    #region ModelOptions Defaults

    [Fact]
    public void ModelOptions_Defaults_ShouldHaveSensibleValues()
    {
        var options = new ModelOptions();

        options.TargetModel.Should().Be("gemini-3.5-flash-high");
        options.FallbackModels.Should().BeEmpty();
        options.AutoSwitchEnabled.Should().BeTrue();
        options.SwitchCommandTemplate.Should().Contain("{model}");
    }

    #endregion

    #region WorkspaceOptions Defaults

    [Fact]
    public void WorkspaceOptions_Defaults_ShouldHaveSensibleValues()
    {
        var options = new WorkspaceOptions();

        options.WorkspacePath.Should().Be(".");
        options.SolutionFile.Should().BeNull();
        options.DetectStrategy.Should().Be(WorkspaceDetectStrategy.Timestamp);
        options.IncludePatterns.Should().NotBeEmpty();
        options.ExcludePatterns.Should().NotBeEmpty();
    }

    #endregion

    #region PromptTemplateOptions Defaults

    [Fact]
    public void PromptTemplateOptions_Defaults_ShouldHaveTemplate()
    {
        var options = new PromptTemplateOptions();

        options.Template.Should().NotBeNullOrWhiteSpace();
        options.Template.Should().Contain("{taskLine}");
        options.Template.Should().Contain("{tasksFile}");
        options.Prefix.Should().BeEmpty();
        options.Suffix.Should().BeEmpty();
        options.Variables.Should().BeEmpty();
    }

    #endregion

    #region CompletionOptions Defaults

    [Fact]
    public void CompletionOptions_Defaults_ShouldHaveMarkers()
    {
        var options = new CompletionOptions();

        options.SuccessMarkers.Should().NotBeEmpty();
        options.FailureMarkers.Should().NotBeEmpty();
        options.TimeoutMarkers.Should().NotBeEmpty();
        options.CaseInsensitive.Should().BeTrue();
    }

    #endregion

    #region ExecutionMode Enum

    [Fact]
    public void ExecutionMode_ShouldContainExpectedValues()
    {
        Enum.GetValues<ExecutionMode>().Should().Contain(ExecutionMode.Sequential);
        Enum.GetValues<ExecutionMode>().Should().Contain(ExecutionMode.Parallel);
    }

    #endregion

    #region WorkspaceDetectStrategy Enum

    [Fact]
    public void WorkspaceDetectStrategy_ShouldContainExpectedValues()
    {
        var values = Enum.GetValues<WorkspaceDetectStrategy>();
        values.Should().Contain(WorkspaceDetectStrategy.Timestamp);
        values.Should().Contain(WorkspaceDetectStrategy.Hash);
        values.Should().Contain(WorkspaceDetectStrategy.Both);
        values.Should().Contain(WorkspaceDetectStrategy.None);
    }

    #endregion
}
