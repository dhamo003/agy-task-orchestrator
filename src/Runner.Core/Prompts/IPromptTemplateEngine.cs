using System.Threading;
using System.Threading.Tasks;
using Runner.Markdown.Models;
using AntigravityTaskRunner.Terminal.Workspace;

namespace AntigravityTaskRunner.Core.Prompts;

public interface IPromptTemplateEngine
{
    Task<string> BuildPromptAsync(TaskItem task, WorkspaceSnapshot initialSnapshot, CancellationToken cancellationToken = default);
}
