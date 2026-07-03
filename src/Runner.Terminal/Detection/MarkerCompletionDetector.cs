using AntigravityTaskRunner.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AntigravityTaskRunner.Terminal.Detection;

/// <summary>
/// Detects task completion by checking the output against configured success and failure markers.
/// </summary>
public class MarkerCompletionDetector : ICompletionDetector
{
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

        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        var relevantLines = lines.ToList();

        // Check failure markers first, as failure takes precedence if both exist somehow
        foreach (var marker in _options.FailureMarkers)
        {
            var failureLine = relevantLines.FirstOrDefault(l => l.Contains(marker, comparison));
            if (failureLine != null)
            {
                _logger.LogInformation("Failure marker detected: {Marker}", marker);
                success = false;
                errorMessage = failureLine.Trim();
                return true;
            }
        }

        // Check success markers
        foreach (var marker in _options.SuccessMarkers)
        {
            if (relevantLines.Any(l => l.Contains(marker, comparison)))
            {
                _logger.LogInformation("Success marker detected: {Marker}", marker);
                success = true;
                return true;
            }
        }

        return false;
    }
}
