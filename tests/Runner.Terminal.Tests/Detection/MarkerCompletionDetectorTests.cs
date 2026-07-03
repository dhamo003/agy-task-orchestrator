using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Terminal.Detection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Runner.Terminal.Tests.Detection;

public class MarkerCompletionDetectorTests
{
    private readonly RunnerOptions _options;
    private readonly MarkerCompletionDetector _detector;

    public MarkerCompletionDetectorTests()
    {
        _options = new RunnerOptions
        {
            Completion = new CompletionOptions
            {
                SuccessMarkers = new List<string> { "[x]", "Task complete" },
                FailureMarkers = new List<string> { "[!]", "FAILED" },
                CaseInsensitive = true
            }
        };

        var optionsWrapper = Options.Create(_options);
        _detector = new MarkerCompletionDetector(optionsWrapper, NullLogger<MarkerCompletionDetector>.Instance);
    }

    [Fact]
    public void DetectCompletion_ShouldReturnTrueAndSuccess_WhenSuccessMarkerPresent()
    {
        var output = "Some output\nThen [x] happens\nDone.";
        
        var result = _detector.DetectCompletion(output, out var success, out var errorMessage);

        Assert.True(result);
        Assert.True(success);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void DetectCompletion_ShouldReturnTrueAndFailure_WhenFailureMarkerPresent()
    {
        var output = "Some output\nFAILED: Something went wrong\nDone.";

        var result = _detector.DetectCompletion(output, out var success, out var errorMessage);

        Assert.True(result);
        Assert.False(success);
        Assert.NotNull(errorMessage);
        Assert.Contains("FAILED: Something went wrong", errorMessage);
    }

    [Fact]
    public void DetectCompletion_ShouldReturnFalse_WhenNoMarkerPresent()
    {
        var output = "Some output\nStill running\nNot done.";

        var result = _detector.DetectCompletion(output, out var success, out var errorMessage);

        Assert.False(result);
        Assert.False(success);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void DetectCompletion_ShouldPrioritizeFailure_WhenBothPresent()
    {
        var output = "FAILED: error occurred\nBut then [x] was printed";

        var result = _detector.DetectCompletion(output, out var success, out var errorMessage);

        Assert.True(result);
        Assert.False(success);
        Assert.NotNull(errorMessage);
        Assert.Contains("FAILED", errorMessage);
    }
}
