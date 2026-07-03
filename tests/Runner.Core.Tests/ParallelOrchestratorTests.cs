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

public class ParallelOrchestratorTests
{
    [Fact]
    public async Task RunAllAsync_ShouldProcessTasksInParallel()
    {
        // Arrange
        var parserMock = new Mock<ITaskParser>();
        var writerMock = new Mock<ITaskWriter>();
        var pipelineMock = new Mock<ITaskPipeline>();
        var retryMock = new Mock<IRetryPolicy>();
        var progressMock = new Mock<IProgressTracker>();
        var loggerMock = new Mock<ITaskLogger>();
        
        var options = new RunnerOptions { TasksFile = "test.md" };
        options.Parallel.MaxWorkers = 2;
        var optionsMock = new Mock<IOptions<RunnerOptions>>();
        optionsMock.Setup(o => o.Value).Returns(options);

        var task1 = new TaskItem(1, "raw", "Task 1", TaskStatus.NotStarted, null, 0);
        var task2 = new TaskItem(2, "raw", "Task 2", TaskStatus.NotStarted, null, 0);
        var phases = new List<TaskPhase> { new TaskPhase("Phase 1", 0, new[] { task1, task2 }, 0) };

        parserMock.Setup(p => p.ParseAsync("test.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(phases);

        retryMock.Setup(r => r.ExecuteAsync(It.IsAny<Func<int, CancellationToken, Task<TaskExecutionResult>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Func<int, CancellationToken, Task<TaskExecutionResult>> act, CancellationToken ct) => 
                new TaskExecutionResult(task1, true, TimeSpan.FromSeconds(1), null, 0));

        var orchestrator = new ParallelOrchestrator(
            parserMock.Object, writerMock.Object, pipelineMock.Object, retryMock.Object, progressMock.Object, loggerMock.Object, optionsMock.Object);

        // Act
        await orchestrator.RunAllAsync(CancellationToken.None);

        // Assert
        parserMock.Verify(p => p.ParseAsync("test.md", It.IsAny<CancellationToken>()), Times.Once);
        retryMock.Verify(r => r.ExecuteAsync(It.IsAny<Func<int, CancellationToken, Task<TaskExecutionResult>>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
