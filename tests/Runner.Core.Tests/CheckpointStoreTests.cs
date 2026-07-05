using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Core.Checkpointing;
using AntigravityTaskRunner.Core.Models;
using AntigravityTaskRunner.Core.Workflow;
using AntigravityTaskRunner.Terminal.Workspace;

namespace AntigravityTaskRunner.Core.Tests;

public sealed class CheckpointStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly JsonCheckpointStore _store;

    public CheckpointStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ckpt-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        var options = new RunnerOptions { WorkspacePath = _dir };
        _store = new JsonCheckpointStore(Microsoft.Extensions.Options.Options.Create(options));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private static ExecutionCheckpoint SampleCheckpoint() =>
        ExecutionCheckpoint.Start(7, "Implement the parser", "gemini-test") with
        {
            State = TaskWorkflowState.Paused,
            Attempt = 2,
            PauseCount = 1,
            Prompt = "the exact prompt sent",
            ModifiedFiles = ["Modified:src/Parser.cs"],
            History =
            [
                new AttemptRecord(1, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow, false,
                    FailureKind.CapacityLimit, "rate limit exceeded"),
            ],
        };

    [Fact]
    public async Task RoundTrips_CompleteCheckpoint()
    {
        await _store.SaveAsync(SampleCheckpoint());

        var loaded = await _store.LoadAsync();

        loaded.Should().NotBeNull();
        loaded!.TaskLine.Should().Be(7);
        loaded.TaskText.Should().Be("Implement the parser");
        loaded.State.Should().Be(TaskWorkflowState.Paused);
        loaded.Attempt.Should().Be(2);
        loaded.PauseCount.Should().Be(1);
        loaded.Prompt.Should().Be("the exact prompt sent");
        loaded.Model.Should().Be("gemini-test");
        loaded.ModifiedFiles.Should().Equal("Modified:src/Parser.cs");
        loaded.History.Should().ContainSingle()
            .Which.Failure.Should().Be(FailureKind.CapacityLimit);
    }

    [Fact]
    public async Task RoundTrips_WorkspaceSnapshot()
    {
        var snapshot = new WorkspaceSnapshot(
            new Dictionary<string, FileSnapshot>
            {
                ["src/A.cs"] = new("src/A.cs", DateTime.UtcNow, 120, "ABC123", "DEF456"),
            },
            DateTime.UtcNow);

        await _store.SaveSnapshotAsync(snapshot);
        var loaded = await _store.LoadSnapshotAsync();

        loaded.Should().NotBeNull();
        loaded!.Files.Should().ContainKey("src/A.cs");
        loaded.Files["src/A.cs"].Hash.Should().Be("ABC123");
        loaded.Files["src/A.cs"].NormalizedHash.Should().Be("DEF456");
    }

    [Fact]
    public async Task Load_ReturnsNull_WhenNothingSaved()
    {
        (await _store.LoadAsync()).Should().BeNull();
        (await _store.LoadSnapshotAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Load_ReturnsNull_OnCorruptFile_InsteadOfThrowing()
    {
        var checkpointPath = Path.Combine(_dir, ".antigravity", "checkpoint.json");
        Directory.CreateDirectory(Path.GetDirectoryName(checkpointPath)!);
        await File.WriteAllTextAsync(checkpointPath, "{ not valid json !!!");

        (await _store.LoadAsync()).Should().BeNull("a corrupt checkpoint must not brick the runner");
    }

    [Fact]
    public async Task Clear_RemovesCheckpointAndSnapshot()
    {
        await _store.SaveAsync(SampleCheckpoint());
        await _store.SaveSnapshotAsync(new WorkspaceSnapshot(new Dictionary<string, FileSnapshot>(), DateTime.UtcNow));

        await _store.ClearAsync();

        (await _store.LoadAsync()).Should().BeNull();
        (await _store.LoadSnapshotAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Matches_IgnoresCheckboxMarkerChanges()
    {
        var checkpoint = ExecutionCheckpoint.Start(3, "My task text", "m");
        checkpoint.Matches(3, "My task text").Should().BeTrue();
        checkpoint.Matches(3, "  My task text ").Should().BeTrue("whitespace-insensitive");
        checkpoint.Matches(4, "My task text").Should().BeFalse("different line");
        checkpoint.Matches(3, "Other task").Should().BeFalse("different text");
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Disabled_Checkpointing_IsNoOp()
    {
        var options = new RunnerOptions { WorkspacePath = _dir };
        options.Checkpoint.Enabled = false;
        var store = new JsonCheckpointStore(Microsoft.Extensions.Options.Options.Create(options));

        await store.SaveAsync(SampleCheckpoint());
        (await store.LoadAsync()).Should().BeNull();
        Directory.Exists(Path.Combine(_dir, ".antigravity")).Should().BeFalse();
    }
}
