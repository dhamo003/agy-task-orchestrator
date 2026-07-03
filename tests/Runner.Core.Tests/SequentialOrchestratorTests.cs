using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Core.Models;
using AntigravityTaskRunner.Core.Orchestration;
using AntigravityTaskRunner.Core.Pipeline;
using AntigravityTaskRunner.Core.Progress;
using AntigravityTaskRunner.Core.Retry;
using Runner.Markdown.Parser;
using Runner.Markdown.Writer;
using Runner.Markdown.Models;
using Runner.Logging;
using TaskStatus = Runner.Markdown.Models.TaskStatus;

namespace AntigravityTaskRunner.Core.Tests;

public class SequentialOrchestratorTests
{
    [Fact]
    public async Task RunAllAsync_ShouldProcessTasksSequentially_UntilNoMoreTasks()
    {
        // Arrange
        var parserMock = new Mock<ITaskParser>();
        var writerMock = new Mock<ITaskWriter>();
        var pipelineMock = new Mock<ITaskPipeline>();
        var retryMock = new Mock<IRetryPolicy>();
        var progressMock = new Mock<IProgressTracker>();
        var loggerMock = new Mock<ITaskLogger>();
        var optionsMock = new Mock<IOptions<RunnerOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new RunnerOptions { TasksFile = "test.md" });

        var task1 = new TaskItem(1, "raw", "Task 1", TaskStatus.NotStarted, null, 0);
        var phases = new List<TaskPhase> { new TaskPhase("Phase 1", 0, new[] { task1 }, 0) };

        parserMock.SetupSequence(p => p.GetNextTask(It.IsAny<IReadOnlyList<TaskPhase>>()))
            .Returns(task1)
            .Returns((TaskItem?)null);

        retryMock.Setup(r => r.ExecuteAsync(It.IsAny<Func<int, CancellationToken, Task<TaskExecutionResult>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaskExecutionResult(task1, true, TimeSpan.FromSeconds(1), null, 0));

        var orchestrator = new SequentialOrchestrator(
            parserMock.Object, 
            writerMock.Object, 
            pipelineMock.Object, 
            retryMock.Object, 
            progressMock.Object, 
            loggerMock.Object, 
            optionsMock.Object);

        // Act
        await orchestrator.RunAllAsync(CancellationToken.None);

        // Assert
        parserMock.Verify(p => p.ParseAsync("test.md", It.IsAny<CancellationToken>()), Times.Exactly(2));
        retryMock.Verify(r => r.ExecuteAsync(It.IsAny<Func<int, CancellationToken, Task<TaskExecutionResult>>>(), It.IsAny<CancellationToken>()), Times.Once);
        progressMock.Verify(p => p.ReportTaskStarted(task1), Times.Once);
        progressMock.Verify(p => p.ReportTaskCompleted(task1, true, It.IsAny<TimeSpan>()), Times.Once);
    }
}
