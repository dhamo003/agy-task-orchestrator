using System.Threading;
using System.Threading.Tasks;
using AntigravityTaskRunner.Core.Models;
using AntigravityTaskRunner.Terminal.Workspace;
using Runner.Markdown.Models;

namespace AntigravityTaskRunner.Core.Pipeline;

/// <summary>
/// Executes one attempt of a single task: fresh agent session → capacity-limit check →
/// workspace verification → build/test validation. Returns a fully classified result;
/// it never writes task status (the orchestrator owns state transitions).
/// </summary>
public interface ITaskPipeline
{
    /// <summary>
    /// Executes one attempt.
    /// </summary>
    /// <param name="task">The task to execute.</param>
    /// <param name="initialSnapshot">
    /// The workspace baseline captured before the FIRST attempt of this task. All
    /// attempts compare against this same baseline so retries see cumulative changes.
    /// </param>
    /// <param name="prompt">The fully-built prompt (includes retry failure context).</param>
    /// <param name="attempt">The 1-based attempt number.</param>
    /// <param name="token">Cancellation token.</param>
    Task<TaskExecutionResult> ExecuteAsync(
        TaskItem task,
        WorkspaceSnapshot initialSnapshot,
        string prompt,
        int attempt,
        CancellationToken token = default);
}
