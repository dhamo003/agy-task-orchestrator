namespace AntigravityTaskRunner.Core.Models;

/// <summary>
/// Classifies why a task attempt did not complete, so retries can target the actual
/// problem and capacity limits can pause (rather than fail) the pipeline.
/// </summary>
public enum FailureKind
{
    /// <summary>The attempt succeeded.</summary>
    None,

    /// <summary>The agent reported TASK_FAILED.</summary>
    AgentReportedFailure,

    /// <summary>No TASK_COMPLETED marker was observed.</summary>
    MarkerMissing,

    /// <summary>No workspace files changed at all.</summary>
    NoChanges,

    /// <summary>Only markdown/comments/formatting/cache/tasks-file changes were made.</summary>
    NoMeaningfulChanges,

    /// <summary>dotnet restore/build failed.</summary>
    BuildFailed,

    /// <summary>Tests failed.</summary>
    TestsFailed,

    /// <summary>The attempt hit the task timeout.</summary>
    Timeout,

    /// <summary>The agent session could not be established (CLI never ready…).</summary>
    SessionFailure,

    /// <summary>A token/rate/quota/context limit was detected — pause, don't fail.</summary>
    CapacityLimit,

    /// <summary>An unexpected exception occurred in the pipeline.</summary>
    Exception
}
