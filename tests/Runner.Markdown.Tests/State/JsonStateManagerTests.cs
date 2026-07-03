using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Runner.Markdown.State;
using Runner.Markdown.Models;
using Xunit;

namespace AntigravityTaskRunner.Markdown.Tests.State;

public class JsonStateManagerTests
{
    [Fact]
    public async Task SaveAndLoadState_WorksCorrectly()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var manager = new JsonStateManager(tempFile);
        var state = new RunnerState(42, DateTimeOffset.UtcNow, 2);

        try
        {
            // Act
            await manager.SaveStateAsync(state);
            var loadedState = await manager.LoadStateAsync();

            // Assert
            loadedState.Should().NotBeNull();
            loadedState!.LastTaskLine.Should().Be(42);
            loadedState.Attempt.Should().Be(2);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ClearState_RemovesFile()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "{}");
        var manager = new JsonStateManager(tempFile);

        try
        {
            // Act
            await manager.ClearStateAsync();

            // Assert
            File.Exists(tempFile).Should().BeFalse();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
