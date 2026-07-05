using System.Text.RegularExpressions;
using AntigravityTaskRunner.Terminal.Sessions;
using Microsoft.Extensions.Logging;

namespace AntigravityTaskRunner.Terminal.Detection;

/// <summary>
/// Detects the current model by parsing the output of a CLI command (e.g. `/model`).
/// </summary>
public partial class OutputModelDetector : IModelDetector
{
    private readonly ILogger<OutputModelDetector> _logger;

    public OutputModelDetector(ILogger<OutputModelDetector> logger)
    {
        _logger = logger;
    }

    public Task<string?> DetectModelAsync(ITerminalSession session, CancellationToken token = default)
    {
        var output = session.GetCurrentOutput().StdOut;
        if (string.IsNullOrWhiteSpace(output))
        {
            return Task.FromResult<string?>(null);
        }

        var knownModels = new[]
        {
            "gemini 3.5 flash (high)",
            "gemini 3.5 flash (medium)",
            "gemini 3.5 flash (low)",
            "gemini 3.1 pro (high)",
            "gemini 3.1 pro (low)",
            "claude sonnet 4.6 (thinking)",
            "claude opus 4.6 (thinking)",
            "gpt-oss 120b (medium)"
        };

        foreach (var m in knownModels)
        {
            var normalizedHyphen = m.Replace(" ", "-").Replace("(", "").Replace(")", "");
            if (output.Contains(m, StringComparison.OrdinalIgnoreCase) ||
                output.Contains(normalizedHyphen, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Detected active CLI model from output: {Model}", m);
                return Task.FromResult<string?>(m);
            }
        }

        return Task.FromResult<string?>(null);
    }


    // Adjust regex based on actual Antigravity CLI output.
    [GeneratedRegex(@"Current model:\s*(?<model>[\w\-\.]+)", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex ModelNameRegex();
}
