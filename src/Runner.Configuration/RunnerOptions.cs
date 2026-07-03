namespace AntigravityTaskRunner.Configuration;

/// <summary>
/// Top-level configuration options for the AntigravityTaskRunner.
/// Binds to the "Runner" section in appsettings.json.
/// </summary>
public sealed class RunnerOptions
{
    /// <summary>
    /// Configuration section name used for binding.
    /// </summary>
    public const string SectionName = "Runner";

    /// <summary>
    /// Path to the tasks markdown file to process.
    /// </summary>
    public string TasksFile { get; set; } = "tasks.md";

    /// <summary>
    /// Target AI model to use for task execution.
    /// </summary>
    public string Model { get; set; } = "gemini-3.5-flash-high";

    /// <summary>
    /// When true, parses and displays tasks without executing them.
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// When true, enables verbose/detailed output during execution.
    /// </summary>
    public bool Verbose { get; set; }

    /// <summary>
    /// Path to the workspace root directory.
    /// </summary>
    public string WorkspacePath { get; set; } = ".";

    /// <summary>
    /// Retry configuration for failed tasks.
    /// </summary>
    public RetryOptions Retry { get; set; } = new();

    /// <summary>
    /// Timeout configuration for tasks and sessions.
    /// </summary>
    public TimeoutOptions Timeout { get; set; } = new();

    /// <summary>
    /// Parallel execution configuration.
    /// </summary>
    public ParallelExecutionOptions Parallel { get; set; } = new();

    /// <summary>
    /// Model selection and switching configuration.
    /// </summary>
    public ModelOptions ModelConfig { get; set; } = new();

    /// <summary>
    /// Workspace analysis configuration.
    /// </summary>
    public WorkspaceOptions Workspace { get; set; } = new();

    /// <summary>
    /// Prompt template configuration for task prompts.
    /// </summary>
    public PromptTemplateOptions PromptTemplate { get; set; } = new();

    /// <summary>
    /// Completion detection configuration.
    /// </summary>
    public CompletionOptions Completion { get; set; } = new();
    /// <summary>
    /// Terminal session configuration.
    /// </summary>
    public TerminalOptions Terminal { get; set; } = new();
}
