using AntigravityTaskRunner.Core;
using Xunit;

namespace AntigravityTaskRunner.Core.Tests;

/// <summary>
/// Placeholder test to ensure test project compiles and is discoverable.
/// Will be replaced by Task F.22.
/// </summary>
public class CorePlaceholderTests
{
    [Fact]
    public void ProjectName_ShouldBeCorrect()
    {
        Assert.Equal("Runner.Core", CorePlaceholder.ProjectName);
    }
}
