using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Terminal.Workspace;
using Microsoft.Extensions.Options;

namespace AntigravityTaskRunner.Core.Checkpointing;

/// <summary>
/// JSON-file checkpoint store with atomic writes (temp file + rename) so a crash
/// mid-write can never corrupt the recovery state.
/// </summary>
public sealed class JsonCheckpointStore : ICheckpointStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly CheckpointOptions _options;
    private readonly string _workspacePath;

    public JsonCheckpointStore(IOptions<RunnerOptions> options)
    {
        _options = options.Value.Checkpoint;
        _workspacePath = string.IsNullOrWhiteSpace(options.Value.WorkspacePath) || options.Value.WorkspacePath == "."
            ? Environment.CurrentDirectory
            : options.Value.WorkspacePath;
    }

    private string Directory => Path.IsPathRooted(_options.Directory)
        ? _options.Directory
        : Path.Combine(_workspacePath, _options.Directory);

    private string CheckpointPath => Path.Combine(Directory, _options.CheckpointFileName);
    private string SnapshotPath => Path.Combine(Directory, _options.SnapshotFileName);

    public Task SaveAsync(ExecutionCheckpoint checkpoint, CancellationToken token = default) =>
        _options.Enabled ? WriteAtomicAsync(CheckpointPath, checkpoint, token) : Task.CompletedTask;

    public Task SaveSnapshotAsync(WorkspaceSnapshot snapshot, CancellationToken token = default) =>
        _options.Enabled ? WriteAtomicAsync(SnapshotPath, snapshot, token) : Task.CompletedTask;

    public async Task<ExecutionCheckpoint?> LoadAsync(CancellationToken token = default) =>
        _options.Enabled ? await ReadAsync<ExecutionCheckpoint>(CheckpointPath, token) : null;

    public async Task<WorkspaceSnapshot?> LoadSnapshotAsync(CancellationToken token = default) =>
        _options.Enabled ? await ReadAsync<WorkspaceSnapshot>(SnapshotPath, token) : null;

    public Task ClearAsync(CancellationToken token = default)
    {
        TryDelete(CheckpointPath);
        TryDelete(SnapshotPath);
        return Task.CompletedTask;
    }

    private async Task WriteAtomicAsync<T>(string path, T value, CancellationToken token)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var tempPath = path + ".tmp";
        var json = JsonSerializer.Serialize(value, SerializerOptions);
        await File.WriteAllTextAsync(tempPath, json, token);
        File.Move(tempPath, path, overwrite: true);
    }

    private static async Task<T?> ReadAsync<T>(string path, CancellationToken token) where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, token);
            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<T>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            // A corrupt checkpoint must not brick the runner; treat as absent.
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
            // best-effort cleanup
        }
    }
}
