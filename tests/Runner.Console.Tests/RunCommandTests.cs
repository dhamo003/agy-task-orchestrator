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
using Runner.Logging;
using Runner.Markdown;
using AntigravityTaskRunner.Terminal;
using AntigravityTaskRunner.Terminal.Sessions;
using AntigravityTaskRunner.Terminal.Detection;
using AntigravityTaskRunner.Terminal.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace Runner.Console.Tests;

[Collection("Sequential")]
public class RunCommandTests : IDisposable
{
    private static readonly string[] SampleWorkspaceChanges = ["src/file.cs"];

    private readonly string _tempDir;
    private readonly string _tasksFile;

    public RunCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _tasksFile = Path.Combine(_tempDir, "tasks.md");
        
        File.WriteAllText(_tasksFile, """
        # Test Tasks
        - [ ] Task 1
        - [ ] Task 2
        """);
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
    public void DryRun_ShouldNotExecuteTasks()
    {
        // Arrange
        var app = CreateApp();
        
        // Act
        var result = app.Run(new[] { "--dry-run", "-t", _tasksFile });
        
        // Assert
        Assert.Equal(0, result);
        var content = File.ReadAllText(_tasksFile);
        Assert.Contains("- [ ] Task 1", content);
        Assert.Contains("- [ ] Task 2", content);
    }

    [Fact]
    public void EndToEnd_WithMockTerminal_ShouldCompleteTasks()
    {
        // Arrange
        var mockTerminal = new Mock<ITerminalSession>();
        mockTerminal.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);
        mockTerminal.Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new TerminalSessionResult(0, "mock output", "", TimeSpan.FromSeconds(1)));
        mockTerminal.Setup(x => x.GetCurrentOutput())
                    .Returns(("SUCCESS_MARKER", ""));

        var mockCompletionDetector = new Mock<ICompletionDetector>();
        bool success = true;
        string? errorMessage = null;
        mockCompletionDetector.Setup(x => x.DetectCompletion(It.IsAny<string>(), out success, out errorMessage))
                              .Returns(true);

        // Report a meaningful workspace change so the pipeline's strict verification treats each task as done.
        var mockWorkspace = new Mock<IWorkspaceAnalyzer>();
        mockWorkspace.Setup(w => w.TakeSnapshotAsync(It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new WorkspaceSnapshot(new Dictionary<string, FileSnapshot>(), DateTime.UtcNow));
        mockWorkspace.Setup(w => w.GetChangeSet(It.IsAny<WorkspaceSnapshot>(), It.IsAny<WorkspaceSnapshot>()))
                     .Returns(new WorkspaceChangeSet(
                         [new FileChange(SampleWorkspaceChanges[0], FileChangeKind.Modified, IsMeaningful: true)]));

        var app = CreateApp(services =>
        {
            // Override the real terminal session with mock
            services.AddSingleton(mockTerminal.Object);
            services.AddSingleton(mockCompletionDetector.Object);
            services.AddSingleton(mockWorkspace.Object);
        });

        // Act — use one-shot mode so completion is driven by the mocked process exit
        // (WaitForExitAsync -> exit 0) rather than interactive terminal-output heuristics.
        var result = app.Run(new[] { "--one-shot", "-t", _tasksFile });

        // Assert
        Assert.Equal(0, result);
        var content = File.ReadAllText(_tasksFile);
        Assert.Contains("- [x] Task 1", content);
        Assert.Contains("- [x] Task 2", content);
    }

    private CommandApp<RunCommand> CreateApp(Action<IServiceCollection>? configureOverrides = null)
    {
        var testConsole = new TestConsole();
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

                services.PostConfigure<WorkspaceOptions>(opts => opts.WorkspacePath = _tempDir);
                services.PostConfigure<RunnerOptions>(opts =>
                {
                    opts.WorkspacePath = _tempDir;
                    // No real dotnet builds inside unit tests.
                    opts.Build.Enabled = false;
                });

                configureOverrides?.Invoke(services);
            });

        var registrations = new ServiceCollection();
        registrations.AddSingleton(hostBuilder);

        var registrar = new TypeRegistrar(registrations);
        var app = new CommandApp<RunCommand>(registrar);
        app.Configure(config =>
        {
            config.ConfigureConsole(testConsole);
        });
        
        return app;
    }
}
