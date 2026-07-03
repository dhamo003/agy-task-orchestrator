using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Runner.Markdown.Models;

namespace Runner.Markdown.State;

public class JsonStateManager : IStateManager
{
    private readonly string _stateFilePath;

    public JsonStateManager(string stateFilePath = "runner-state.json")
    {
        _stateFilePath = stateFilePath;
    }

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public async Task SaveStateAsync(RunnerState state, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(state, Options);
        await File.WriteAllTextAsync(_stateFilePath, json, cancellationToken);
    }

    public async Task<RunnerState?> LoadStateAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_stateFilePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(_stateFilePath, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<RunnerState>(json);
    }

    public Task ClearStateAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_stateFilePath))
        {
            File.Delete(_stateFilePath);
        }
        return Task.CompletedTask;
    }
}
