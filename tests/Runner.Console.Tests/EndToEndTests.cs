using System;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace Runner.Console.Tests;

[Collection("Sequential")]
public class EndToEndTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _tasksFile;

    public EndToEndTests()
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
    public void ResumeAfterInterruption_ShouldSkipCompletedTasks()
    {
        // Arrange
        File.WriteAllText(_tasksFile, """
        # Test Tasks
        - [x] Task 1
        - [ ] Task 2
        """);

        var mockTerminal = new Mock<ITerminalSession>();
        mockTerminal.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);
        // Simulate output that gets detected as completed
        mockTerminal.Setup(x => x.GetCurrentOutput())
                    .Returns(("SUCCESS_MARKER", ""));

        var mockCompletionDetector = new Mock<ICompletionDetector>();
        bool success = true;
        string? errorMessage = null;
        mockCompletionDetector.Setup(x => x.DetectCompletion(It.IsAny<string>(), out success, out errorMessage))
                              .Returns(true);

        var app = CreateApp(services => 
        {
            services.AddTransient(sp => mockTerminal.Object);
            services.AddSingleton(mockCompletionDetector.Object);
        });

        // Act
        var result = app.Run(new[] { "-t", _tasksFile });

        // Assert
        Assert.Equal(0, result);
        var content = File.ReadAllText(_tasksFile);
        Assert.Contains("- [x] Task 1", content);
        Assert.Contains("- [x] Task 2", content);
        
        // Terminal session should have only been started once (for Task 2)
        mockTerminal.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void MultiTaskSequential_ShouldCompleteAllPendingTasks()
    {
        // Arrange
        File.WriteAllText(_tasksFile, """
        # Test Tasks
        - [ ] Task 1
        - [ ] Task 2
        """);

        var mockTerminal = new Mock<ITerminalSession>();
        mockTerminal.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);
        mockTerminal.Setup(x => x.GetCurrentOutput())
                    .Returns(("SUCCESS_MARKER", ""));

        var mockCompletionDetector = new Mock<ICompletionDetector>();
        bool success = true;
        string? errorMessage = null;
        mockCompletionDetector.Setup(x => x.DetectCompletion(It.IsAny<string>(), out success, out errorMessage))
                              .Returns(true);

        var app = CreateApp(services => 
        {
            services.AddTransient(sp => mockTerminal.Object);
            services.AddSingleton(mockCompletionDetector.Object);
        });

        // Act
        var result = app.Run(new[] { "-t", _tasksFile });

        // Assert
        Assert.Equal(0, result);
        var content = File.ReadAllText(_tasksFile);
        Assert.Contains("- [x] Task 1", content);
        Assert.Contains("- [x] Task 2", content);

        // Terminal session should have been started twice
        mockTerminal.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
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
