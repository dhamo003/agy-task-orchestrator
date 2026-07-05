using System;
using AntigravityTaskRunner.Core.Verification;
using AntigravityTaskRunner.Terminal.Build;
using AntigravityTaskRunner.Terminal.Detection;
using Runner.Markdown.Models;

namespace AntigravityTaskRunner.Core.Models;

/// <summary>
/// Represents the result of a single task attempt, including everything needed to
/// build an intelligent retry prompt or a final failure report.
/// </summary>
/// <param name="Task">The task that ran.</param>
/// <param name="IsSuccess">True only when every verification and build check passed.</param>
/// <param name="Duration">Attempt duration.</param>
/// <param name="ErrorMessage">Primary human-readable failure reason.</param>
/// <param name="RetryCount">The 1-based attempt number that produced this result.</param>
/// <param name="Failure">Machine-readable failure classification.</param>
/// <param name="Verification">The verification report, when verification ran.</param>
/// <param name="Build">The build/test validation result, when it ran.</param>
/// <param name="Limit">The capacity-limit detection, when one was found.</param>
public record TaskExecutionResult(
    TaskItem Task,
    bool IsSuccess,
    TimeSpan Duration,
    string? ErrorMessage,
    int RetryCount,
    FailureKind Failure = FailureKind.None,
    VerificationReport? Verification = null,
    BuildValidationResult? Build = null,
    LimitDetection? Limit = null)
{
    /// <summary>True when this attempt hit a capacity limit and should pause, not fail.</summary>
    public bool IsCapacityLimit => Failure == FailureKind.CapacityLimit;
}
