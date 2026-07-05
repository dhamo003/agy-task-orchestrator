using AntigravityTaskRunner.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AntigravityTaskRunner.Terminal.Detection;

/// <summary>
/// Case-insensitive substring matcher over the configured limit patterns.
/// </summary>
public sealed class PatternLimitDetector : ILimitDetector
{
    private readonly LimitOptions _options;
    private readonly ILogger<PatternLimitDetector> _logger;

    public PatternLimitDetector(IOptions<RunnerOptions> options, ILogger<PatternLimitDetector> logger)
    {
        _options = options.Value.Limits;
        _logger = logger;
    }

    public LimitDetection? Detect(string output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return null;
        }

        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            foreach (var pattern in _options.LimitPatterns)
            {
                if (line.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Capacity limit detected (pattern '{Pattern}'): {Line}", pattern, line.Trim());
                    return new LimitDetection(pattern, line.Trim());
                }
            }
        }

        return null;
    }
}
