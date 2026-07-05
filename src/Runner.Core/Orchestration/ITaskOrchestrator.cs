using System.Threading;
using System.Threading.Tasks;
using AntigravityTaskRunner.Core.Models;
using Runner.Markdown.Models;

namespace AntigravityTaskRunner.Core.Orchestration;

/// <summary>
/// Orchestrates the execution of tasks. Strictly sequential: exactly one task (and one
/// agent session) is ever active, and execution halts — never skips — on failure.
/// </summary>
public interface ITaskOrchestrator
{
    /// <summary>
    /// Runs pending tasks one at a time until all complete, a task fails permanently
    /// (the pipeline halts), or cancellation is requested.
    /// </summary>
    Task RunAllAsync(CancellationToken token = default);

    /// <summary>
    /// Runs a single task through the full workflow (Pending → Running → Verifying →
    /// Completed/Failed) including retries, pauses, and checkpointing.
    /// </summary>
    Task<TaskExecutionResult> RunSingleAsync(TaskItem task, CancellationToken token = default);

    /// <summary>
    /// Stops the execution gracefully.
    /// </summary>
    Task StopAsync();
}
