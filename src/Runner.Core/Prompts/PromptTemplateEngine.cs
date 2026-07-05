using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Core.Retry;
using Runner.Markdown.Models;
using AntigravityTaskRunner.Terminal.Workspace;

namespace AntigravityTaskRunner.Core.Prompts;

public class PromptTemplateEngine : IPromptTemplateEngine
{
    private readonly RunnerOptions _options;

    public PromptTemplateEngine(IOptions<RunnerOptions> options)
    {
        _options = options.Value;
    }

    public Task<string> BuildPromptAsync(
        TaskItem task,
        WorkspaceSnapshot initialSnapshot,
        RetryContext? retryContext = null,
        CancellationToken cancellationToken = default)
    {
        var templateOptions = _options.PromptTemplate;
        var sb = new StringBuilder();

        // 1. Prefix
        if (!string.IsNullOrWhiteSpace(templateOptions.Prefix))
        {
            sb.AppendLine(templateOptions.Prefix);
        }

        // 2. Main template replacement
        var prompt = templateOptions.Template
            .Replace("{task}", task.DisplayText)
            .Replace("{taskLine}", task.RawText.Trim())
            .Replace("{lineNumber}", task.LineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("{tasksFile}", _options.TasksFile)
            .Replace("{workspace}", _options.WorkspacePath);

        // 3. Workspace context injection
        if (prompt.Contains("{workspaceContext}"))
        {
            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine("Files in workspace:");
            foreach (var file in initialSnapshot.Files.Keys)
            {
                contextBuilder.Append("- ").AppendLine(file);
            }
            prompt = prompt.Replace("{workspaceContext}", contextBuilder.ToString());
        }

        // 4. Custom variables replacement
        if (templateOptions.Variables != null)
        {
            foreach (var kvp in templateOptions.Variables)
            {
                prompt = prompt.Replace($"{{{kvp.Key}}}", kvp.Value);
            }
        }

        sb.AppendLine(prompt);

        // 5. Retry failure context: tell the AI exactly what went wrong last time.
        if (retryContext is { IsRetry: true })
        {
            var guidance = retryContext.BuildGuidance();
            if (guidance.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("---- PREVIOUS ATTEMPT FAILURE CONTEXT ----");
                sb.AppendLine(guidance);
                sb.AppendLine("---- END FAILURE CONTEXT ----");
            }
        }

        // 6. Suffix (task-only scope enforcement can be placed here)
        if (!string.IsNullOrWhiteSpace(templateOptions.Suffix))
        {
            sb.AppendLine();
            sb.AppendLine(templateOptions.Suffix);
        }

        return Task.FromResult(sb.ToString().TrimEnd());
    }
}
