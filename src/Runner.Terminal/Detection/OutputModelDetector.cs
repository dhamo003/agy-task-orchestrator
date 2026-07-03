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
        _logger.LogDebug("Model detection is bypassed (interactive menu is required). Falling back to force switch.");
        return Task.FromResult<string?>(null);
    }


    // Adjust regex based on actual Antigravity CLI output.
    [GeneratedRegex(@"Current model:\s*(?<model>[\w\-\.]+)", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex ModelNameRegex();
}
