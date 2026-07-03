using System.Threading;
using System.Threading.Tasks;
using Runner.Markdown.Models;

namespace AntigravityTaskRunner.Core.Orchestration;

/// <summary>
/// Orchestrates the execution of tasks.
/// </summary>
public interface ITaskOrchestrator
{
    /// <summary>
    /// Runs all pending tasks.
    /// </summary>
    /// <param name="token">Cancellation token.</param>
    Task RunAllAsync(CancellationToken token = default);

    /// <summary>
    /// Runs a single task.
    /// </summary>
    /// <param name="task">The task to run.</param>
    /// <param name="token">Cancellation token.</param>
    Task RunSingleAsync(TaskItem task, CancellationToken token = default);

    /// <summary>
    /// Stops the execution gracefully.
    /// </summary>
    Task StopAsync();
}
