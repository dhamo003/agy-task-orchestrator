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
    /// </summary>
    private const string DefaultTemplate =
        "/goal Read {tasksFile}. Find the single checklist line that exactly matches:\n" +
        "'{taskLine}'\n\n" +
        "Complete ONLY that task, verifying your work before finishing.\n" +
        "Do not start or touch any other task in the file, even if it seems related.\n" +
        "When you finish, print the word 'TASK_' followed by 'COMPLETED'.\n" +
        "When you cannot complete it, print the word 'TASK_' followed by 'FAILED'.";

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
