using System;
using System.Collections.Generic;
using Moq;
using Xunit;
using FluentAssertions;
using AntigravityTaskRunner.Core.Workflow;
using Runner.Logging;

namespace AntigravityTaskRunner.Core.Tests;

public class TaskStateMachineTests
{
    private static TaskStateMachine Build(TaskWorkflowState initial = TaskWorkflowState.Pending) =>
        new(new Mock<ITaskLogger>().Object, new TaskLogScope("T-1", "Task", 1), initial);

    [Fact]
    public void HappyPath_PendingRunningVerifyingCompleted()
    {
        var machine = Build();

        machine.TransitionTo(TaskWorkflowState.Running);
        machine.TransitionTo(TaskWorkflowState.Verifying);
        machine.TransitionTo(TaskWorkflowState.Completed);

        machine.State.Should().Be(TaskWorkflowState.Completed);
        machine.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void RetryLoop_VerifyingBackToRunning_IsLegal()
    {
        var machine = Build();
        machine.TransitionTo(TaskWorkflowState.Running);
        machine.TransitionTo(TaskWorkflowState.Verifying);
        machine.TransitionTo(TaskWorkflowState.Running, "retry");
        machine.State.Should().Be(TaskWorkflowState.Running);
    }

    [Fact]
    public void PauseResume_RunningPausedRunning_IsLegal()
    {
        var machine = Build();
        machine.TransitionTo(TaskWorkflowState.Running);
        machine.TransitionTo(TaskWorkflowState.Paused, "capacity limit");
        machine.TransitionTo(TaskWorkflowState.Running, "resumed");
        machine.State.Should().Be(TaskWorkflowState.Running);
    }

    [Theory]
    [InlineData(TaskWorkflowState.Pending, TaskWorkflowState.Verifying)]
    [InlineData(TaskWorkflowState.Pending, TaskWorkflowState.Completed)]
    [InlineData(TaskWorkflowState.Pending, TaskWorkflowState.Paused)]
    [InlineData(TaskWorkflowState.Running, TaskWorkflowState.Completed)]
    [InlineData(TaskWorkflowState.Running, TaskWorkflowState.Pending)]
    [InlineData(TaskWorkflowState.Paused, TaskWorkflowState.Completed)]
    public void IllegalTransitions_Throw(TaskWorkflowState from, TaskWorkflowState to)
    {
        var machine = Build(from);
        var act = () => machine.TransitionTo(to);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{from} → {to}*");
    }

    [Theory]
    [InlineData(TaskWorkflowState.Completed)]
    [InlineData(TaskWorkflowState.Failed)]
    public void TerminalStates_AllowNoFurtherTransitions(TaskWorkflowState terminal)
    {
        var machine = Build(terminal);
        machine.IsTerminal.Should().BeTrue();
        foreach (var next in Enum.GetValues<TaskWorkflowState>())
        {
            machine.CanTransitionTo(next).Should().BeFalse();
        }
    }

    [Fact]
    public void Transitions_RaiseEvent_WithDetail()
    {
        var machine = Build();
        var events = new List<(TaskWorkflowState From, TaskWorkflowState To, string? Detail)>();
        machine.Transitioned += (from, to, detail) => events.Add((from, to, detail));

        machine.TransitionTo(TaskWorkflowState.Running, "attempt 1");

        events.Should().ContainSingle();
        events[0].Should().Be((TaskWorkflowState.Pending, TaskWorkflowState.Running, "attempt 1"));
    }
}
