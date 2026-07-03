namespace AntigravityTaskRunner.Core.Models;

using System.Threading;
using Runner.Markdown.Models;
using AntigravityTaskRunner.Terminal.Workspace;

/// <summary>
/// Contains context for the execution of a single task.
/// </summary>
public record TaskExecutionContext(
    TaskItem Task,
    int Attempt,
    WorkspaceSnapshot InitialWorkspaceSnapshot,
    CancellationToken CancellationToken
);
