using System;
using Runner.Markdown.Models;

namespace AntigravityTaskRunner.Core.Models;

/// <summary>
/// Represents the result of a single task's execution.
/// </summary>
public record TaskExecutionResult(
    TaskItem Task,
    bool IsSuccess,
    TimeSpan Duration,
    string? ErrorMessage,
    int RetryCount
);
