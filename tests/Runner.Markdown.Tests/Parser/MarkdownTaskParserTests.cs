using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Runner.Markdown.Parser;
using Runner.Markdown.Models;
using Xunit;
using TaskStatus = Runner.Markdown.Models.TaskStatus;

namespace AntigravityTaskRunner.Markdown.Tests.Parser;

public class MarkdownTaskParserTests
{
    private readonly MarkdownTaskParser _parser = new();

    [Fact]
    public async Task ParseAsync_ParsesBasicCheckboxes()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var content = @"
- [ ] Task 1 Not Started
- [x] Task 2 Completed
- [/] Task 3 In Progress
- [!] Task 4 Failed
- [-] Task 5 Skipped
";
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            // Act
            var phases = await _parser.ParseAsync(tempFile);

            // Assert
            phases.Should().HaveCount(1);
            var phase = phases[0];
            phase.Name.Should().Be("Default");
            phase.Tasks.Should().HaveCount(5);

            phase.Tasks[0].Status.Should().Be(TaskStatus.NotStarted);
            phase.Tasks[0].DisplayText.Should().Be("Task 1 Not Started");
            
            phase.Tasks[1].Status.Should().Be(TaskStatus.Completed);
            phase.Tasks[2].Status.Should().Be(TaskStatus.InProgress);
            phase.Tasks[3].Status.Should().Be(TaskStatus.Failed);
            phase.Tasks[4].Status.Should().Be(TaskStatus.Skipped);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ParseAsync_GroupsByPhases()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var content = @"
- [x] **Phase A**
  - [x] Task A1
- [ ] **Phase B**
  - [/] Task B1
  - [ ] Task B2
";
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            // Act
            var phases = await _parser.ParseAsync(tempFile);

            // Assert
            phases.Should().HaveCount(2);
            phases[0].Name.Should().Be("Phase A");
            phases[0].Tasks.Should().HaveCount(1);
            phases[0].Tasks[0].DisplayText.Should().Be("Task A1");
            phases[0].CompletionPercentage.Should().Be(100);

            phases[1].Name.Should().Be("Phase B");
            phases[1].Tasks.Should().HaveCount(2);
            phases[1].Tasks[0].Status.Should().Be(TaskStatus.InProgress);
            phases[1].CompletionPercentage.Should().Be(0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task GetNextTask_FindsFirstUnchecked()
    {
        var tempFile = Path.GetTempFileName();
        var content = @"
- [x] **Phase A**
  - [x] Task A1
- [ ] **Phase B**
  - [/] Task B1
  - [ ] Task B2
";
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            var phases = await _parser.ParseAsync(tempFile);
            var nextTask = _parser.GetNextTask(phases);

            nextTask.Should().NotBeNull();
            nextTask!.DisplayText.Should().Be("Task B1");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
