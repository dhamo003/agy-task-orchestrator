using System.Threading;
using System.Threading.Tasks;
using Runner.Markdown.Models;
using TaskStatus = Runner.Markdown.Models.TaskStatus;

namespace Runner.Markdown.Writer;

public interface ITaskWriter
{
    Task UpdateStatusAsync(string filePath, TaskItem task, TaskStatus newStatus, string? reason = null, CancellationToken cancellationToken = default);
}
