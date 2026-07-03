using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FluentAssertions;
using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Core.Models;
using AntigravityTaskRunner.Core.Pipeline;
using AntigravityTaskRunner.Terminal.Sessions;
using AntigravityTaskRunner.Terminal.Workspace;
using AntigravityTaskRunner.Terminal.Detection;
using Runner.Markdown.Writer;
using Runner.Markdown.Models;
using Runner.Logging;
using TaskStatus = Runner.Markdown.Models.TaskStatus;

namespace AntigravityTaskRunner.Core.Tests;

public class TaskPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldRunAllSteps_OnSuccess()
    {
        // Arrange
        var serviceProviderMock = new Mock<IServiceProvider>();
        var terminalMock = new Mock<ITerminalSession>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ITerminalSession))).Returns(terminalMock.Object);

        var workspaceMock = new Mock<IWorkspaceAnalyzer>();
        workspaceMock.Setup(w => w.TakeSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceSnapshot(new Dictionary<string, FileSnapshot>(), DateTime.UtcNow));
        workspaceMock.Setup(w => w.GetChanges(It.IsAny<WorkspaceSnapshot>(), It.IsAny<WorkspaceSnapshot>()))
            .Returns(new List<string> { "file.cs" });

        var modelDetectorMock = new Mock<IModelDetector>();
        modelDetectorMock.Setup(m => m.DetectModelAsync(It.IsAny<ITerminalSession>(), It.IsAny<CancellationToken>())).ReturnsAsync("gemini-3.5-flash-high");

        var modelSwitcherMock = new Mock<IModelSwitcher>();
        var completionDetectorMock = new Mock<ICompletionDetector>();
        
        bool success = true;
        string? errorMessage = null;
        completionDetectorMock.Setup(c => c.DetectCompletion(It.IsAny<string>(), out success, out errorMessage))
            .Returns(true);

        var taskWriterMock = new Mock<ITaskWriter>();
        var loggerMock = new Mock<ITaskLogger>();

        var options = new RunnerOptions { TasksFile = "tasks.md", Model = "gemini-3.5-flash-high" };
        var optionsMock = new Mock<IOptions<RunnerOptions>>();
        optionsMock.Setup(o => o.Value).Returns(options);

        var promptTemplateEngineMock = new Mock<AntigravityTaskRunner.Core.Prompts.IPromptTemplateEngine>();
        promptTemplateEngineMock.Setup(p => p.BuildPromptAsync(It.IsAny<TaskItem>(), It.IsAny<WorkspaceSnapshot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Mock Prompt");

        var pipeline = new TaskPipeline(
            serviceProviderMock.Object,
            workspaceMock.Object,
            modelDetectorMock.Object,
            modelSwitcherMock.Object,
            completionDetectorMock.Object,
            taskWriterMock.Object,
            loggerMock.Object,
            promptTemplateEngineMock.Object,
            optionsMock.Object
        );

        var taskItem = new TaskItem(1, "raw", "Task 1", TaskStatus.NotStarted, null, 0);

        terminalMock.Setup(t => t.GetCurrentOutput()).Returns(("Output", ""));

        // Act
        var result = await pipeline.ExecuteAsync(taskItem, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        workspaceMock.Verify(w => w.TakeSnapshotAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        terminalMock.Verify(t => t.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        terminalMock.Verify(t => t.SendInputAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        taskWriterMock.Verify(t => t.UpdateStatusAsync("tasks.md", taskItem, TaskStatus.Completed, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
