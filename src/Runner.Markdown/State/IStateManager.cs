using System.Threading;
using System.Threading.Tasks;
using Runner.Markdown.Models;

namespace Runner.Markdown.State;

public interface IStateManager
{
    Task SaveStateAsync(RunnerState state, CancellationToken cancellationToken = default);
    Task<RunnerState?> LoadStateAsync(CancellationToken cancellationToken = default);
    Task ClearStateAsync(CancellationToken cancellationToken = default);
}
