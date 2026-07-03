using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Runner.Markdown.Writer;
using Runner.Markdown.Models;
using Xunit;
using TaskStatus = Runner.Markdown.Models.TaskStatus;
using System;
using System.Linq;

namespace AntigravityTaskRunner.Markdown.Tests.Writer;

public class MarkdownTaskWriterTests
{
    private readonly MarkdownTaskWriter _writer = new();

    [Fact]
    public async Task UpdateStatusAsync_ChangesStatusCorrectly()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var content = @"
- [ ] Task 1 Not Started
- [ ] Task 2
";
        await File.WriteAllTextAsync(tempFile, content);

        var taskItem = new TaskItem(2, "- [ ] Task 1 Not Started", "Task 1 Not Started", TaskStatus.NotStarted, "Default", 0);

        try
        {
            // Act
            await _writer.UpdateStatusAsync(tempFile, taskItem, TaskStatus.Completed);

            // Assert
            var lines = await File.ReadAllLinesAsync(tempFile);
            lines[1].Should().StartWith("- [x] Task 1 Not Started");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task UpdateStatusAsync_AddsReasonOnFailure()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var content = @"- [ ] Task 1 Not Started";
        await File.WriteAllTextAsync(tempFile, content);

        var taskItem = new TaskItem(1, "- [ ] Task 1 Not Started", "Task 1 Not Started", TaskStatus.NotStarted, "Default", 0);

        try
        {
            // Act
            await _writer.UpdateStatusAsync(tempFile, taskItem, TaskStatus.Failed, "Timeout");

            // Assert
            var text = await File.ReadAllTextAsync(tempFile);
            text.Should().Contain("- [!] Task 1 Not Started");
            text.Should().Contain("(Reason: Timeout)");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
