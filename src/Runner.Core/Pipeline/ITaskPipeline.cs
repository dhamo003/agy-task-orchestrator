using System.Threading;
using System.Threading.Tasks;
using AntigravityTaskRunner.Core.Models;
using Runner.Markdown.Models;

namespace AntigravityTaskRunner.Core.Pipeline;

/// <summary>
/// Defines a pipeline for executing a single task from start to finish.
/// </summary>
public interface ITaskPipeline
{
    /// <summary>
    /// Executes the task.
    /// </summary>
    /// <param name="task">The task to execute.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>The result of the task execution.</returns>
    Task<TaskExecutionResult> ExecuteAsync(TaskItem task, CancellationToken token = default);
}
