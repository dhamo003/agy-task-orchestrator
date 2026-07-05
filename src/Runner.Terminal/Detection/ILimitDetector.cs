namespace AntigravityTaskRunner.Terminal.Detection;

/// <summary>
/// Details of a detected AI capacity limit (token/rate/quota/context overflow).
/// </summary>
/// <param name="MatchedPattern">The configured pattern that matched.</param>
/// <param name="MatchedText">The output line the pattern matched in (trimmed).</param>
public sealed record LimitDetection(string MatchedPattern, string MatchedText);

/// <summary>
/// Detects token limits, rate limits, quota exhaustion, and context overflow in agent
/// output so execution can pause and resume instead of failing or skipping the task.
/// </summary>
public interface ILimitDetector
{
    /// <summary>
    /// Scans <paramref name="output"/> for capacity-limit indicators.
    /// Returns the first detection, or null when none match.
    /// </summary>
    LimitDetection? Detect(string output);
}
