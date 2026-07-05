using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using FluentAssertions;
using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Core.Models;
using AntigravityTaskRunner.Core.Retry;
using AntigravityTaskRunner.Terminal.Detection;
using Runner.Markdown.Models;
using Runner.Logging;
using TaskStatus = Runner.Markdown.Models.TaskStatus;

namespace AntigravityTaskRunner.Core.Tests;

public class RetryPolicyTests
{
    private static readonly TaskItem Task1 = new(1, "- [ ] Task", "Task", TaskStatus.NotStarted, null, 0);
    private static readonly int[] SameAttemptTwice = [1, 1];

    private static RetryPolicy BuildPolicy(int maxRetries, int pauseSeconds = 1, int maxPauses = 3)
    {
        var options = new RunnerOptions();
        options.Retry.MaxRetries = maxRetries;
        options.Retry.BackoffBaseSeconds = 0.001;
        options.Retry.BackoffMaxSeconds = 0.002;
        options.Retry.UseJitter = false;
        options.Limits.PauseSeconds = pauseSeconds;
        options.Limits.MaxPausesPerTask = maxPauses;
        return new RetryPolicy(
            Microsoft.Extensions.Options.Options.Create(options),
            new Mock<ITaskLogger>().Object);
    }

    private static TaskExecutionResult Success(int attempt) =>
        new(Task1, true, TimeSpan.Zero, null, attempt);

    private static TaskExecutionResult Failure(int attempt, FailureKind kind = FailureKind.MarkerMissing) =>
        new(Task1, false, TimeSpan.Zero, $"fail {kind}", attempt, kind);

    private static TaskExecutionResult Limited(int attempt) =>
        new(Task1, false, TimeSpan.Zero, "rate limit exceeded", attempt, FailureKind.CapacityLimit,
            Limit: new LimitDetection("rate limit exceeded", "429 rate limit exceeded"));

    [Fact]
    public async Task Succeeds_FirstAttempt_NoRetries()
    {
        var policy = BuildPolicy(maxRetries: 3);
        int calls = 0;

        var result = await policy.ExecuteAsync((ctx, _) =>
        {
            calls++;
            return Task.FromResult(Success(ctx.Attempt));
        });

        result.IsSuccess.Should().BeTrue();
        calls.Should().Be(1);
    }

    [Fact]
    public async Task Retries_CarryFailureContext_ToNextAttempt()
    {
        var policy = BuildPolicy(maxRetries: 2);
        var contexts = new List<RetryContext>();

        var result = await policy.ExecuteAsync((ctx, _) =>
        {
            contexts.Add(ctx);
            return Task.FromResult(ctx.Attempt < 3 ? Failure(ctx.Attempt) : Success(ctx.Attempt));
        });

        result.IsSuccess.Should().BeTrue();
        contexts.Should().HaveCount(3);
        contexts[0].IsRetry.Should().BeFalse();
        contexts[1].IsRetry.Should().BeTrue();
        contexts[1].PreviousResult!.Failure.Should().Be(FailureKind.MarkerMissing);
        contexts[1].BuildGuidance().Should().Contain("previous attempt FAILED");
        contexts[2].Attempt.Should().Be(3);
    }

    [Fact]
    public async Task Exhaustion_ReturnsLastFailure()
    {
        var policy = BuildPolicy(maxRetries: 2);
        int calls = 0;

        var result = await policy.ExecuteAsync((ctx, _) =>
        {
            calls++;
            return Task.FromResult(Failure(ctx.Attempt, FailureKind.BuildFailed));
        });

        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(FailureKind.BuildFailed);
        calls.Should().Be(3, "1 initial + 2 retries");
    }

    [Fact]
    public async Task CapacityLimit_DoesNotConsumeRetry_AndResumesSameAttempt()
    {
        var policy = BuildPolicy(maxRetries: 0, pauseSeconds: 1);
        var attempts = new List<int>();
        int calls = 0;

        var result = await policy.ExecuteAsync((ctx, _) =>
        {
            attempts.Add(ctx.Attempt);
            return Task.FromResult(++calls == 1 ? Limited(ctx.Attempt) : Success(ctx.Attempt));
        });

        result.IsSuccess.Should().BeTrue();
        attempts.Should().Equal(SameAttemptTwice, "a pause must not advance the attempt number");
    }

    [Fact]
    public async Task CapacityLimit_GivesUp_AfterMaxPauses()
    {
        var policy = BuildPolicy(maxRetries: 0, pauseSeconds: 1, maxPauses: 2);
        int calls = 0;

        var result = await policy.ExecuteAsync((ctx, _) =>
        {
            calls++;
            return Task.FromResult(Limited(ctx.Attempt));
        });

        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(FailureKind.CapacityLimit);
        calls.Should().Be(3, "initial + 2 allowed pauses, then give up");
    }

    [Fact]
    public async Task Observer_IsInvokedAfterEveryAttempt()
    {
        var policy = BuildPolicy(maxRetries: 1);
        var observed = new List<(FailureKind Kind, int Attempt)>();

        await policy.ExecuteAsync(
            (ctx, _) => Task.FromResult(ctx.Attempt == 1 ? Failure(1) : Success(2)),
            onAttemptCompleted: (result, ctx) =>
            {
                observed.Add((result.Failure, ctx.Attempt));
                return Task.CompletedTask;
            });

        observed.Should().Equal((FailureKind.MarkerMissing, 1), (FailureKind.None, 2));
    }

    [Fact]
    public async Task InitialContext_ResumesAtGivenAttempt()
    {
        var policy = BuildPolicy(maxRetries: 5);
        var attempts = new List<int>();

        await policy.ExecuteAsync(
            (ctx, _) =>
            {
                attempts.Add(ctx.Attempt);
                return Task.FromResult(Success(ctx.Attempt));
            },
            initialContext: new RetryContext(4, null));

        attempts.Should().ContainSingle("only one attempt was needed")
            .Which.Should().Be(4, "crash recovery must resume at the checkpointed attempt");
    }
}
