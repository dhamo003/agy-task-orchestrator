using System;
using System.Collections.Generic;
using Runner.Logging;

namespace AntigravityTaskRunner.Core.Workflow;

/// <summary>
/// Enforces the task workflow finite-state machine and emits a structured log entry
/// for every transition. Any illegal transition throws, which turns silent workflow
/// bugs (e.g. starting a second task while one is running) into loud failures.
/// </summary>
public sealed class TaskStateMachine
{
    private static readonly Dictionary<TaskWorkflowState, TaskWorkflowState[]> AllowedTransitions =
        new()
        {
            [TaskWorkflowState.Pending] = [TaskWorkflowState.Running, TaskWorkflowState.Failed],
            [TaskWorkflowState.Running] = [TaskWorkflowState.Verifying, TaskWorkflowState.Paused, TaskWorkflowState.Failed],
            [TaskWorkflowState.Verifying] = [TaskWorkflowState.Completed, TaskWorkflowState.Running, TaskWorkflowState.Paused, TaskWorkflowState.Failed],
            [TaskWorkflowState.Paused] = [TaskWorkflowState.Running, TaskWorkflowState.Failed],
            [TaskWorkflowState.Completed] = [],
            [TaskWorkflowState.Failed] = [],
        };

    private readonly ITaskLogger _logger;
    private readonly TaskLogScope _scope;

    public TaskStateMachine(ITaskLogger logger, TaskLogScope scope, TaskWorkflowState initialState = TaskWorkflowState.Pending)
    {
        _logger = logger;
        _scope = scope;
        State = initialState;
    }

    /// <summary>The current workflow state.</summary>
    public TaskWorkflowState State { get; private set; }

    /// <summary>True when the task reached a terminal state.</summary>
    public bool IsTerminal => State is TaskWorkflowState.Completed or TaskWorkflowState.Failed;

    /// <summary>Raised after every successful transition (old state, new state, detail).</summary>
    public event Action<TaskWorkflowState, TaskWorkflowState, string?>? Transitioned;

    /// <summary>
    /// Moves the machine to <paramref name="next"/>, logging the transition.
    /// Throws <see cref="InvalidOperationException"/> when the transition is illegal.
    /// </summary>
    public void TransitionTo(TaskWorkflowState next, string? detail = null)
    {
        if (!CanTransitionTo(next))
        {
            var message = $"Illegal workflow transition {State} → {next}" +
                          (detail is null ? "" : $" ({detail})");
            _logger.LogError(_scope, message);
            throw new InvalidOperationException(message);
        }

        var previous = State;
        State = next;
        _logger.LogInfo(_scope, FormatTransition(previous, next, detail));
        Transitioned?.Invoke(previous, next, detail);
    }

    /// <summary>Returns true when moving to <paramref name="next"/> is legal from the current state.</summary>
    public bool CanTransitionTo(TaskWorkflowState next) =>
        AllowedTransitions.TryGetValue(State, out var allowed) && Array.IndexOf(allowed, next) >= 0;

    private static string FormatTransition(TaskWorkflowState from, TaskWorkflowState to, string? detail) =>
        detail is null
            ? $"[workflow] {from} → {to}"
            : $"[workflow] {from} → {to} — {detail}";
}
