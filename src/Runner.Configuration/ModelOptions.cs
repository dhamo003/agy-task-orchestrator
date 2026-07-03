namespace AntigravityTaskRunner.Configuration;

/// <summary>
/// Configuration for AI model selection and automatic switching.
/// Binds to the "Runner:ModelConfig" section in appsettings.json.
/// </summary>
public sealed class ModelOptions
{
    /// <summary>
    /// The primary target model name to use for task execution.
    /// </summary>
    public string TargetModel { get; set; } = "gemini-3.5-flash-high";

    /// <summary>
    /// Ordered list of fallback model names to try if the target model is unavailable.
    /// </summary>
    public List<string> FallbackModels { get; set; } = [];

    /// <summary>
    /// When true, automatically switches to the target model if the CLI starts with a different model.
    /// </summary>
    public bool AutoSwitchEnabled { get; set; } = true;

    /// <summary>
    /// The CLI command/flag format used to switch models.
    /// Use {model} as a placeholder for the model name.
    /// </summary>
    public string SwitchCommandTemplate { get; set; } = "--model {model}";
}
