namespace AntigravityTaskRunner.Configuration;

/// <summary>
/// Configuration for the prompt template used to instruct the AI model per task.
/// Binds to the "Runner:PromptTemplate" section in appsettings.json.
/// Supported placeholders: {task}, {taskLine}, {tasksFile}, {workspace}, {file}.
/// </summary>
public sealed class PromptTemplateOptions
{
    /// <summary>
    /// Default prompt template with standard placeholders.
    /// The completion markers are QUOTED on purpose: interactive CLIs echo the prompt
    /// back into the terminal, and the quotes guarantee that any echoed (or line-wrapped)
    /// fragment of these instructions can never look like a genuine standalone marker
    /// line to the completion detector.
    /// </summary>
    private const string DefaultTemplate =
        "Read {tasksFile}. Find the single checklist line that matches: '{taskLine}'. " +
        "Complete ONLY that task by writing or modifying code and tests. Verify your work. " +
        "When fully finished and verified, print a final line containing exactly \"TASK_COMPLETED\" (without the quotes, no other text on that line). " +
        "If you cannot complete the task, print a final line \"TASK_FAILED: <reason>\" (without the quotes).";

    /// <summary>
    /// The prompt template string with placeholders.
    /// Supported placeholders: {task}, {taskLine}, {tasksFile}, {workspace}, {file}.
    /// </summary>
    public string Template { get; set; } = DefaultTemplate;

    /// <summary>
    /// Optional text prepended before the resolved template.
    /// </summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// Optional text appended after the resolved template.
    /// </summary>
    public string Suffix { get; set; } = string.Empty;

    /// <summary>
    /// Additional custom variables that can be referenced in the template as {key}.
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = [];
}
