using System.Text.RegularExpressions;
using AntigravityTaskRunner.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AntigravityTaskRunner.Terminal.Detection;

/// <summary>
/// Detects task completion by checking the output against configured success and
/// failure markers.
///
/// A marker only counts when it is printed STANDALONE at the start of a line (after
/// stripping terminal/markdown decorations). This is critical: the task prompt itself
/// contains the marker words ("…print TASK_COMPLETED. If unable…, print TASK_FAILED."),
/// and interactive CLIs echo the prompt back into the output. A naive substring match
/// would "detect" completion from the echo the instant the prompt is sent — before the
/// agent has done any work. Standalone-line matching rejects echoed instruction text
/// (which has surrounding words) and wrapped echo fragments (which carry the trailing
/// period from the instruction sentence), while still accepting the agent's own
/// "TASK_COMPLETED", "**TASK_COMPLETED**", or "TASK_FAILED: reason" lines.
/// </summary>
public class MarkerCompletionDetector : ICompletionDetector
{
    /// <summary>
    /// ANSI/VT escape sequences (colors, cursor movement, OSC titles…) that a PTY
    /// stream interleaves with the text. They must be stripped before line analysis,
    /// otherwise a marker rendered as "\e[32mTASK_COMPLETED\e[0m" never appears to
    /// start the line.
    /// </summary>
    private static readonly Regex AnsiEscapes = new(
        @"\x1B(?:\[[0-9;:?]*[ -/]*[@-~]|\][^\x07\x1B]*(?:\x07|\x1B\\)|[@-Z\\-_])",
        RegexOptions.Compiled);

    /// <summary>Characters the terminal UI / markdown may draw around the agent's marker line.</summary>
    private static readonly char[] LeadingDecorations =
        [' ', '\t', '>', '?', '|', '│', '┃', '║', '─', '┌', '└', '├', '┤', '*', '#', '`', '✦', '✧', '·', '•', '-', '~'];

    private static readonly char[] TrailingDecorations = [' ', '\t', '*', '`', '|', '│', '┃', '║'];

    private readonly CompletionOptions _options;
    private readonly ILogger<MarkerCompletionDetector> _logger;

    public MarkerCompletionDetector(IOptions<RunnerOptions> options, ILogger<MarkerCompletionDetector> logger)
    {
        _options = options.Value.Completion;
        _logger = logger;
    }

    public bool DetectCompletion(string output, out bool success, out string? errorMessage)
    {
        success = false;
        errorMessage = null;

        var comparison = _options.CaseInsensitive
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var cleaned = output.Contains('\x1B') ? AnsiEscapes.Replace(output, "") : output;
        var lines = cleaned.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // Check failure markers first: failure takes precedence if both exist somehow.
        foreach (var line in lines)
        {
            var normalized = StripDecorations(line);
            foreach (var marker in _options.FailureMarkers)
            {
                if (IsStandaloneMarker(normalized, marker, comparison))
                {
                    _logger.LogInformation("Failure marker detected: {Marker}", marker);
                    success = false;
                    errorMessage = normalized;
                    return true;
                }
            }
        }

        foreach (var line in lines)
        {
            var normalized = StripDecorations(line);
            foreach (var marker in _options.SuccessMarkers)
            {
                if (IsStandaloneMarker(normalized, marker, comparison))
                {
                    _logger.LogInformation("Success marker detected: {Marker}", marker);
                    success = true;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// True when <paramref name="line"/> is the marker printed on its own line:
    /// the line must START with the marker, and what follows may only be a
    /// ":" reason suffix or non-alphanumeric decoration. A "." immediately after
    /// the marker is rejected — that is the signature of the echoed prompt sentence
    /// ("…print TASK_FAILED.") or a line-wrapped fragment of it.
    /// </summary>
    private static bool IsStandaloneMarker(string line, string marker, StringComparison comparison)
    {
        if (!line.StartsWith(marker, comparison))
        {
            return false;
        }

        var rest = line[marker.Length..].TrimEnd();
        if (rest.Length == 0)
        {
            return true; // exactly the marker
        }

        if (rest[0] is '.' or '"' or '\'' or '”' or '’')
        {
            // Echo signatures: the instruction sentence ends the marker with a period
            // ("…print TASK_FAILED.") or, with the quoted default template, a closing
            // quote ("…print \"TASK_COMPLETED\"…"). Wrapped fragments keep those chars.
            return false;
        }

        if (rest[0] == ':')
        {
            // "MARKER: reason" — but an echoed/wrapped fragment of the quoted template
            // instruction ("…print \"TASK_FAILED: <reason>\"…") always carries a quote
            // character in its tail; a genuine agent reason line does not.
            return !rest.Contains('"') && !rest.Contains('”') && !rest.Contains("<reason>", StringComparison.OrdinalIgnoreCase);
        }

        // Allow trailing decorations/emphasis only — no additional words.
        return !rest.Any(char.IsLetterOrDigit);
    }

    private static string StripDecorations(string line)
    {
        var trimmed = line.Trim();
        trimmed = trimmed.TrimStart(LeadingDecorations);
        trimmed = trimmed.TrimEnd(TrailingDecorations);
        return trimmed;
    }
}
