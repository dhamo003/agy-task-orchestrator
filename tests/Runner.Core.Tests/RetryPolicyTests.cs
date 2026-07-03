using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

using Moq;
using Xunit;
using FluentAssertions;
using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Core.Models;
using AntigravityTaskRunner.Core.Retry;
using Runner.Markdown.Models;
using TaskStatus = Runner.Markdown.Models.TaskStatus;
using Runner.Logging;
namespace AntigravityTaskRunner.Core.Tests;

public class RetryPolicyTests
{
    private readonly RetryOptions _options;
    private readonly RetryPolicy _policy;

    public RetryPolicyTests()
    {
        _options = new RetryOptions { MaxRetries = 2, BackoffBaseSeconds = 0.01, UseJitter = false };
        var optionsMock = new Mock<IOptions<RetryOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_options);
        var loggerMock = new Mock<ITaskLogger>();
        _policy = new RetryPolicy(optionsMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnResultImmediately_OnSuccess()
    {
        var taskItem = new TaskItem(1, "raw", "display", TaskStatus.NotStarted, null, 0);
        var successResult = new TaskExecutionResult(taskItem, true, TimeSpan.Zero, null, 0);
        
        int attempts = 0;
        var result = await _policy.ExecuteAsync((attempt, ct) => 
        {
            attempts++;
            return Task.FromResult(successResult);
        });

        result.IsSuccess.Should().BeTrue();
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRetry_OnFailure()
    {
        var taskItem = new TaskItem(1, "raw", "display", TaskStatus.NotStarted, null, 0);
        var failureResult = new TaskExecutionResult(taskItem, false, TimeSpan.Zero, "error", 0);
        var successResult = new TaskExecutionResult(taskItem, true, TimeSpan.Zero, null, 1);
        
        int attempts = 0;
        var result = await _policy.ExecuteAsync((attempt, ct) => 
        {
            attempts++;
            return Task.FromResult(attempts == 2 ? successResult : failureResult);
        });

        result.IsSuccess.Should().BeTrue();
        attempts.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFail_AfterMaxRetries()
    {
        var taskItem = new TaskItem(1, "raw", "display", TaskStatus.NotStarted, null, 0);
        var failureResult = new TaskExecutionResult(taskItem, false, TimeSpan.Zero, "error", 0);
        
        int attempts = 0;
        var result = await _policy.ExecuteAsync((attempt, ct) => 
        {
            attempts++;
            return Task.FromResult(failureResult);
        });

        result.IsSuccess.Should().BeFalse();
        attempts.Should().Be(3); // Initial + 2 retries
    }
}
