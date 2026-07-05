using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AntigravityTaskRunner.Console;
using AntigravityTaskRunner.Console.Commands;
using AntigravityTaskRunner.Console.Infrastructure;
using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Core;
using AntigravityTaskRunner.Terminal;
using AntigravityTaskRunner.Terminal.Detection;
using AntigravityTaskRunner.Terminal.Sessions;
using AntigravityTaskRunner.Terminal.Workspace;
using Runner.Logging;
using Runner.Markdown;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace Runner.Console.Tests;

/// <summary>
/// Regression tests that prove tasks are executed strictly one-by-one, with never more than a
/// single Antigravity CLI session alive at any instant.
///
/// The <see cref="TrackingTerminalSession"/> models the real-world failure: disposing the PTY
/// wrapper alone does NOT stop the underlying process (in production, killing the shell left the
/// agy child process tree running). The session's "process" is only considered terminated once
/// <see cref="ITerminalSession.KillAsync"/> / <see cref="ITerminalSession.WaitForExitAsync"/> is
/// invoked. Therefore this test fails if the pipeline moves on to the next task without explicitly
/// driving the current session to a real exit — which is exactly the overlapping-session bug.
/// </summary>
[Collection("Sequential")]
public sealed class SequentialNonOverlapTests : IDisposable
{
    private static readonly string[] SampleWorkspaceChanges = ["src/file.cs"];

    private readonly string _tempDir;
    private readonly string _tasksFile;

    public SequentialNonOverlapTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _tasksFile = Path.Combine(_tempDir, "tasks.md");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ThreeTasks_ShouldRunStrictlyOneAtATime_WithNoOverlap()
    {
        // Arrange: three pending tasks.
        File.WriteAllText(_tasksFile, """
        # Test Tasks
        - [ ] Task 1
        - [ ] Task 2
        - [ ] Task 3
        """);

        var tracker = new SessionTracker();

        var app = CreateApp(tracker);

        // Act
        var result = app.Run(new[] { "-t", _tasksFile });

        // Assert: strict sequential execution.
        Assert.Equal(0, result);

        // The crux: at no instant were two Antigravity sessions alive simultaneously.
        Assert.Equal(1, tracker.MaxConcurrent);

        // Exactly one fresh session was started per task (each on its first attempt).
        Assert.Equal(3, tracker.StartedCount);

        // Every session was driven to a genuine exit; none leaked past its task.
        Assert.Equal(0, tracker.CurrentlyAlive);

        // Every task completed.
        var content = File.ReadAllText(_tasksFile);
        Assert.Contains("- [x] Task 1", content);
        Assert.Contains("- [x] Task 2", content);
        Assert.Contains("- [x] Task 3", content);
    }

    private CommandApp<RunCommand> CreateApp(SessionTracker tracker)
    {
        var testConsole = new TestConsole();

        var completionDetector = new Mock<ICompletionDetector>();
        bool success = true;
        string? errorMessage = null;
        completionDetector
            .Setup(x => x.DetectCompletion(It.IsAny<string>(), out success, out errorMessage))
            .Returns(true);

        // Report a meaningful workspace change so the strict verification treats each task as succeeded.
        var workspaceAnalyzer = new Mock<IWorkspaceAnalyzer>();
        workspaceAnalyzer
            .Setup(w => w.TakeSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceSnapshot(new Dictionary<string, FileSnapshot>(), DateTime.UtcNow));
        workspaceAnalyzer
            .Setup(w => w.GetChangeSet(It.IsAny<WorkspaceSnapshot>(), It.IsAny<WorkspaceSnapshot>()))
            .Returns(new WorkspaceChangeSet(
                [new FileChange(SampleWorkspaceChanges[0], FileChangeKind.Modified, IsMeaningful: true)]));

        // Detector reports the configured model so no model switch (and its delay) occurs.
        var modelDetector = new Mock<IModelDetector>();
        modelDetector
            .Setup(m => m.DetectModelAsync(It.IsAny<ITerminalSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-model");

        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddRunnerConfiguration(context.Configuration);
                services.AddTaskLogging(_tempDir);
                services.AddMarkdownEngine();
                services.AddTerminalServices();
                services.AddRunnerCore();

                services.AddSingleton<IAnsiConsole>(testConsole);
                services.AddHostedService<OrchestratorHostedService>();

                // A brand-new tracking session per task (transient), all sharing one tracker.
                services.AddTransient<ITerminalSession>(_ => new TrackingTerminalSession(tracker));
                services.AddSingleton(completionDetector.Object);
                services.AddSingleton(workspaceAnalyzer.Object);
                services.AddSingleton(modelDetector.Object);

                services.PostConfigure<WorkspaceOptions>(opts => opts.WorkspacePath = _tempDir);
                services.PostConfigure<RunnerOptions>(opts =>
                {
                    opts.Model = "test-model";
                    opts.WorkspacePath = _tempDir;
                    // Remove artificial waits so the test runs quickly and deterministically.
                    opts.Timeout.MinPromptProcessingSeconds = 0;
                    opts.Timeout.IdleSilenceTimeoutSeconds = 0;
                    // No real dotnet builds inside unit tests.
                    opts.Build.Enabled = false;
                });
            });

        var registrations = new ServiceCollection();
        registrations.AddSingleton(hostBuilder);

        var registrar = new TypeRegistrar(registrations);
        var app = new CommandApp<RunCommand>(registrar);
        app.Configure(config => config.ConfigureConsole(testConsole));

        return app;
    }

    /// <summary>
    /// Thread-safe accounting of how many terminal "processes" are alive at once.
    /// </summary>
    private sealed class SessionTracker
    {
        private readonly object _lock = new();
        private int _alive;

        public int MaxConcurrent { get; private set; }
        public int StartedCount { get; private set; }

        public int CurrentlyAlive
        {
            get { lock (_lock) { return _alive; } }
        }

        public void OnProcessStarted()
        {
            lock (_lock)
            {
                _alive++;
                StartedCount++;
                if (_alive > MaxConcurrent)
                {
                    MaxConcurrent = _alive;
                }
            }
        }

        public void OnProcessExited()
        {
            lock (_lock)
            {
                if (_alive > 0)
                {
                    _alive--;
                }
            }
        }
    }

    /// <summary>
    /// Fake session whose underlying "process" survives <see cref="Dispose"/> and is only
    /// terminated by an explicit <see cref="KillAsync"/> / <see cref="WaitForExitAsync"/>.
    /// </summary>
    private sealed class TrackingTerminalSession : ITerminalSession
    {
        private readonly SessionTracker _tracker;
        private bool _alive;

        public TrackingTerminalSession(SessionTracker tracker) => _tracker = tracker;

        public void ConfigureSpawn(string app, IReadOnlyList<string> commandLine, string? workingDirectory)
        {
            // No-op: the fake does not launch a real process.
        }

        public Task StartAsync(CancellationToken token = default)
        {
            _alive = true;
            _tracker.OnProcessStarted();
            return Task.CompletedTask;
        }

        public Task SendInputAsync(string input, CancellationToken token = default) => Task.CompletedTask;

        public (string StdOut, string StdErr) GetCurrentOutput() =>
            ("booting agy...\nTASK_COMPLETED\n? for shortcuts", string.Empty);

        public void ClearOutputBuffers()
        {
        }

        public Task<TerminalSessionResult> WaitForExitAsync(CancellationToken token = default)
        {
            MarkExited();
            return Task.FromResult(new TerminalSessionResult(0, string.Empty, string.Empty, TimeSpan.Zero));
        }

        public Task KillAsync(CancellationToken token = default)
        {
            MarkExited();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            // Deliberately does NOT terminate the process — mirrors the production bug where
            // disposing the PTY wrapper left the agy process tree running.
        }

        private void MarkExited()
        {
            if (_alive)
            {
                _alive = false;
                _tracker.OnProcessExited();
            }
        }
    }
}
