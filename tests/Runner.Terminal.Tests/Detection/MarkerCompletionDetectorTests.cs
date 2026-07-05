using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Terminal.Detection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Runner.Terminal.Tests.Detection;

public class MarkerCompletionDetectorTests
{
    private static MarkerCompletionDetector BuildDetector(
        List<string>? successMarkers = null,
        List<string>? failureMarkers = null)
    {
        var options = new RunnerOptions
        {
            Completion = new CompletionOptions
            {
                SuccessMarkers = successMarkers ?? ["TASK_COMPLETED"],
                FailureMarkers = failureMarkers ?? ["TASK_FAILED"],
                CaseInsensitive = true,
            },
        };

        return new MarkerCompletionDetector(
            Options.Create(options), NullLogger<MarkerCompletionDetector>.Instance);
    }

    [Fact]
    public void Detects_SuccessMarker_OnItsOwnLine()
    {
        var output = "Some agent output\nTASK_COMPLETED\nbye";

        var result = BuildDetector().DetectCompletion(output, out var success, out var errorMessage);

        Assert.True(result);
        Assert.True(success);
        Assert.Null(errorMessage);
    }

    [Theory]
    [InlineData("> TASK_COMPLETED")]
    [InlineData("  **TASK_COMPLETED**")]
    [InlineData("│ TASK_COMPLETED")]
    [InlineData("task_completed")]
    public void Detects_SuccessMarker_WithTerminalOrMarkdownDecorations(string line)
    {
        var result = BuildDetector().DetectCompletion(line, out var success, out _);

        Assert.True(result);
        Assert.True(success);
    }

    [Fact]
    public void Detects_FailureMarker_WithReasonSuffix()
    {
        var output = "TASK_FAILED: could not locate the target file";

        var result = BuildDetector().DetectCompletion(output, out var success, out var errorMessage);

        Assert.True(result);
        Assert.False(success);
        Assert.Contains("could not locate", errorMessage);
    }

    [Fact]
    public void IgnoresEchoedPromptInstruction_ContainingBothMarkers()
    {
        // The interactive CLI echoes the prompt back into the terminal output. This
        // exact text used to trigger an instant false TASK_FAILED before the agent
        // had done any work (production regression).
        var output =
            "> Read tasks.md. Focus on the task on line 153.\n" +
            "  finished and verified, print TASK_COMPLETED. If unable to complete, print TASK_FAILED.\n" +
            "?  Generating...";

        var result = BuildDetector().DetectCompletion(output, out _, out _);

        Assert.False(result, "echoed prompt instructions must never count as markers");
    }

    [Theory]
    [InlineData("TASK_FAILED.")]                     // line-wrapped echo fragment keeps the sentence period
    [InlineData("TASK_COMPLETED. If unable to complete")]
    [InlineData("When finished print TASK_COMPLETED")] // marker not at line start
    [InlineData("Then TASK_FAILED happens")]
    [InlineData("TASK_COMPLETED\" (without the quotes, no other text on that line).")] // quoted-template echo fragment
    [InlineData("TASK_FAILED: <reason>\" (without the quotes).")]
    [InlineData("TASK_COMPLETED\"")]                 // wrap fragment carrying the closing quote
    public void IgnoresEchoFragments_AndMidSentenceMentions(string line)
    {
        var result = BuildDetector().DetectCompletion(line, out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void IgnoresEchoOfQuotedDefaultTemplate()
    {
        // The new default template quotes the markers; the full echoed instruction
        // must never register as a marker.
        var output =
            "> Complete ONLY this task. When fully finished and verified, print a final line containing\n" +
            "  exactly \"TASK_COMPLETED\" (without the quotes, no other text on that line). If unable to\n" +
            "  complete, print a final line \"TASK_FAILED: <reason>\" (without the quotes).\n" +
            "?  Generating...";

        Assert.False(BuildDetector().DetectCompletion(output, out _, out _));
    }

    [Theory]
    [InlineData("\u001b[38;5;186mTASK_COMPLETED\u001b[0m")]              // colored marker line
    [InlineData("\u001b[2K\u001b[1GTASK_COMPLETED")]                      // erase-line + cursor-move prefix
    [InlineData("  \u001b[1mTASK_COMPLETED\u001b[22m  ")]                 // bold with padding
    public void Detects_Marker_WrappedInAnsiEscapeSequences(string line)
    {
        // PTY streams interleave ANSI escapes with the text; a genuine marker line must
        // still be recognized (production regression: agent printed TASK_COMPLETED but
        // detection missed it in the raw terminal buffer).
        var result = BuildDetector().DetectCompletion(line, out var success, out _);

        Assert.True(result);
        Assert.True(success);
    }

    [Fact]
    public void Detects_Marker_InsideLargeRealWorldTranscript()
    {
        var transcript =
            new string('x', 50_000) + "\n" +
            "### Progress Impact\n" +
            "  Phase 3B (Database Schema Discovery) is completed.\n" +
            "  TASK_COMPLETED\n" +
            "? for shortcuts";

        var result = BuildDetector().DetectCompletion(transcript, out var success, out _);

        Assert.True(result);
        Assert.True(success);
    }

    [Fact]
    public void ReturnsFalse_WhenNoMarkerPresent()
    {
        var output = "Some output\nStill running\nNot done.";

        var result = BuildDetector().DetectCompletion(output, out var success, out var errorMessage);

        Assert.False(result);
        Assert.False(success);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void PrioritizesFailure_WhenBothMarkersPresent()
    {
        var output = "TASK_FAILED: error occurred\nTASK_COMPLETED";

        var result = BuildDetector().DetectCompletion(output, out var success, out var errorMessage);

        Assert.True(result);
        Assert.False(success);
        Assert.Contains("TASK_FAILED", errorMessage);
    }

    [Fact]
    public void SupportsCustomMarkers()
    {
        var detector = BuildDetector(successMarkers: ["[x]"], failureMarkers: ["[!]"]);

        Assert.True(detector.DetectCompletion("[x]", out var success, out _));
        Assert.True(success);

        Assert.True(detector.DetectCompletion("[!]: boom", out success, out var error));
        Assert.False(success);
        Assert.Contains("boom", error);
    }
}
