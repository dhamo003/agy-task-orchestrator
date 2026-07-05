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

    /// <summary>
    /// Completion-verification engine configuration.
    /// </summary>
    public VerificationOptions Verification { get; set; } = new();

    /// <summary>
    /// Post-attempt build &amp; test validation configuration.
    /// </summary>
    public BuildValidationOptions Build { get; set; } = new();

    /// <summary>
    /// Token/rate/quota limit detection and pause/resume configuration.
    /// </summary>
    public LimitOptions Limits { get; set; } = new();

    /// <summary>
    /// Persistent checkpointing / crash-recovery configuration.
    /// </summary>
    public CheckpointOptions Checkpoint { get; set; } = new();

    /// <summary>
    /// When true, a task previously marked failed ([!]) is reset to pending and
    /// re-attempted on startup. When false (default) the orchestrator halts on a
    /// failed task so nothing is ever silently skipped.
    /// </summary>
    public bool RetryFailedTasks { get; set; }
}
