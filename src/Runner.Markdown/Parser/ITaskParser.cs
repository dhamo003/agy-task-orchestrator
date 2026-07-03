using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Runner.Markdown.Models;

namespace Runner.Markdown.Parser;

public interface ITaskParser
{
    Task<IReadOnlyList<TaskPhase>> ParseAsync(string filePath, CancellationToken cancellationToken = default);
    TaskItem? GetNextTask(IReadOnlyList<TaskPhase> phases);
}
