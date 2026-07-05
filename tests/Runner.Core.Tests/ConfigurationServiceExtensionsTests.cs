using AntigravityTaskRunner.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AntigravityTaskRunner.Core.Tests;

/// <summary>
/// Integration tests for <see cref="ConfigurationServiceExtensions.AddRunnerConfiguration"/>.
/// Verifies that configuration binding and validation wiring works end-to-end.
/// </summary>
public class ConfigurationServiceExtensionsTests
{
    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    [Fact]
    public void AddRunnerConfiguration_WithValidConfig_ShouldResolveOptions()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Runner:TasksFile"] = "my-tasks.md",
            ["Runner:Model"] = "claude-3-opus",
            ["Runner:DryRun"] = "true",
            ["Runner:Verbose"] = "true",
            ["Runner:WorkspacePath"] = "/workspace",
        });

        var services = new ServiceCollection();
        services.AddRunnerConfiguration(config);
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<RunnerOptions>>().Value;

        options.TasksFile.Should().Be("my-tasks.md");
        options.Model.Should().Be("claude-3-opus");
        options.DryRun.Should().BeTrue();
        options.Verbose.Should().BeTrue();
        options.WorkspacePath.Should().Be("/workspace");
    }

    [Fact]
    public void AddRunnerConfiguration_WithNestedRetryConfig_ShouldBindCorrectly()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Runner:TasksFile"] = "tasks.md",
            ["Runner:Model"] = "test-model",
            ["Runner:Retry:MaxRetries"] = "5",
            ["Runner:Retry:BackoffBaseSeconds"] = "10",
            ["Runner:Retry:BackoffMaxSeconds"] = "600",
            ["Runner:Retry:UseJitter"] = "false",
        });

        var services = new ServiceCollection();
        services.AddRunnerConfiguration(config);
        var provider = services.BuildServiceProvider();

        var retryOptions = provider.GetRequiredService<IOptions<RetryOptions>>().Value;

        retryOptions.MaxRetries.Should().Be(5);
        retryOptions.BackoffBaseSeconds.Should().Be(10);
        retryOptions.BackoffMaxSeconds.Should().Be(600);
        retryOptions.UseJitter.Should().BeFalse();
    }

    [Fact]
    public void AddRunnerConfiguration_WithProductionSections_ShouldBindCorrectly()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Runner:TasksFile"] = "tasks.md",
            ["Runner:Model"] = "test-model",
            ["Runner:Build:Enabled"] = "false",
            ["Runner:Limits:PauseSeconds"] = "120",
            ["Runner:Limits:MaxPausesPerTask"] = "5",
            ["Runner:Checkpoint:Directory"] = ".custom-ckpt",
            ["Runner:Verification:RequireMeaningfulDiff"] = "false",
        });

        var services = new ServiceCollection();
        services.AddRunnerConfiguration(config);
        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<BuildValidationOptions>>().Value.Enabled.Should().BeFalse();
        var limits = provider.GetRequiredService<IOptions<LimitOptions>>().Value;
        limits.PauseSeconds.Should().Be(120);
        limits.MaxPausesPerTask.Should().Be(5);
        provider.GetRequiredService<IOptions<CheckpointOptions>>().Value.Directory.Should().Be(".custom-ckpt");
        provider.GetRequiredService<IOptions<VerificationOptions>>().Value.RequireMeaningfulDiff.Should().BeFalse();
    }

    [Fact]
    public void AddRunnerConfiguration_ShouldRegisterValidator()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Runner:TasksFile"] = "tasks.md",
            ["Runner:Model"] = "test-model",
        });

        var services = new ServiceCollection();
        services.AddRunnerConfiguration(config);
        var provider = services.BuildServiceProvider();

        var validators = provider.GetServices<IValidateOptions<RunnerOptions>>();
        validators.Should().ContainSingle(v => v is RunnerOptionsValidator);
    }

    [Fact]
    public void AddRunnerConfiguration_WithInvalidConfig_ValidatorShouldCatchErrors()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Runner:TasksFile"] = "",
            ["Runner:Model"] = "",
        });

        var services = new ServiceCollection();
        services.AddRunnerConfiguration(config);
        var provider = services.BuildServiceProvider();

        var action = () => provider.GetRequiredService<IOptions<RunnerOptions>>().Value;

        action.Should().Throw<OptionsValidationException>()
            .WithMessage("*TasksFile*")
            .WithMessage("*Model*");
    }
}
