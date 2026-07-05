using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Terminal.Detection;

namespace AntigravityTaskRunner.Terminal.Tests.Detection;

public class PatternLimitDetectorTests
{
    private static PatternLimitDetector Build()
    {
        var options = new RunnerOptions();
        return new PatternLimitDetector(
            Microsoft.Extensions.Options.Options.Create(options),
            new Mock<ILogger<PatternLimitDetector>>().Object);
    }

    [Theory]
    [InlineData("Error: rate limit exceeded, retry after 60s", "rate limit exceeded")]
    [InlineData("HTTP 429 Too Many Requests received", "too many requests")]
    [InlineData("API quota exceeded for this project", "quota exceeded")]
    [InlineData("RESOURCE_EXHAUSTED: try later", "resource_exhausted")]
    [InlineData("The maximum context length was reached", "maximum context length")]
    [InlineData("the model is overloaded right now", "model is overloaded")]
    public void Detects_KnownLimitIndicators(string output, string expectedPattern)
    {
        var detection = Build().Detect(output);

        detection.Should().NotBeNull();
        detection!.MatchedPattern.Should().Be(expectedPattern);
        detection.MatchedText.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("All tests passed, TASK_COMPLETED")]
    [InlineData("Implemented the RateLimiter class in src/RateLimiter.cs")]
    [InlineData("")]
    public void NoDetection_OnNormalOutput(string output)
    {
        Build().Detect(output).Should().BeNull();
    }

    [Fact]
    public void Detection_IsCaseInsensitive()
    {
        Build().Detect("RATE LIMIT EXCEEDED").Should().NotBeNull();
    }

    [Fact]
    public void ReturnsFirstMatchingLine()
    {
        var detection = Build().Detect("line one is fine\nquota exceeded here\nrate limit exceeded later");
        detection!.MatchedText.Should().Be("quota exceeded here");
    }
}
