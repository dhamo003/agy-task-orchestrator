using System.Threading;
using System.Threading.Tasks;
using AntigravityTaskRunner.Core.Retry;
using AntigravityTaskRunner.Terminal.Workspace;
using Runner.Markdown.Models;

namespace AntigravityTaskRunner.Core.Prompts;

/// <summary>
/// Builds the per-task prompt. On retries the prompt additionally carries the previous
/// failure reason, verification/build errors, and file-change facts so the AI corrects
/// the actual problem instead of repeating the same attempt.
/// </summary>
public interface IPromptTemplateEngine
{
    Task<string> BuildPromptAsync(
        TaskItem task,
        WorkspaceSnapshot initialSnapshot,
        RetryContext? retryContext = null,
        CancellationToken cancellationToken = default);
}
