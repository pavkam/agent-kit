// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Delegation;



/// <summary>Verifies TaskDelegationChildResult behavior and contracts.</summary>
public sealed class TaskDelegationChildResultTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var result = new TaskDelegationChildResult(DelegationId(), GoalId(), AgentId(), SessionId(), AttemptId(), RunId(), TaskDelegationStatus.Succeeded, "done", SideEffectCertainty.DefinitelyPerformed);
        result.Id.ShouldBe(DelegationId());
        result.ChildGoalId.ShouldBe(GoalId());
        result.ChildAgentId.ShouldBe(AgentId());
        result.ChildSessionId.ShouldBe(SessionId());
        result.ChildAttemptId.ShouldBe(AttemptId());
        result.ChildRunId.ShouldBe(RunId());
        result.Status.ShouldBe(TaskDelegationStatus.Succeeded);
        result.Summary.ShouldBe("done");
        result.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyPerformed);
    }

    [Fact]
    public void Constructor_WhenChildGoalIdIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new TaskDelegationChildResult(DelegationId(), default, AgentId(), SessionId(), null, null, TaskDelegationStatus.Succeeded, "done", SideEffectCertainty.DefinitelyPerformed));
        exception.ParamName.ShouldBe("childGoalId");
    }

    [Fact]
    public void Constructor_WhenChildAgentIdIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new TaskDelegationChildResult(DelegationId(), GoalId(), default, SessionId(), null, null, TaskDelegationStatus.Succeeded, "done", SideEffectCertainty.DefinitelyPerformed));
        exception.ParamName.ShouldBe("childAgentId");
    }

    [Fact]
    public void Constructor_WhenChildSessionIdIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new TaskDelegationChildResult(DelegationId(), GoalId(), AgentId(), default, null, null, TaskDelegationStatus.Succeeded, "done", SideEffectCertainty.DefinitelyPerformed));
        exception.ParamName.ShouldBe("childSessionId");
    }

    [Fact]
    public void Constructor_WhenStatusIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new TaskDelegationChildResult(DelegationId(), GoalId(), AgentId(), SessionId(), null, null, (TaskDelegationStatus) 999, "done", SideEffectCertainty.DefinitelyPerformed));
        exception.ParamName.ShouldBe("status");
    }

    [Fact]
    public void Constructor_WhenSummaryIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new TaskDelegationChildResult(DelegationId(), GoalId(), AgentId(), SessionId(), null, null, TaskDelegationStatus.Succeeded, " ", SideEffectCertainty.DefinitelyPerformed));
        exception.ParamName.ShouldBe("summary");
    }

    [Fact]
    public void Constructor_WhenSideEffectCertaintyIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new TaskDelegationChildResult(DelegationId(), GoalId(), AgentId(), SessionId(), null, null, TaskDelegationStatus.Succeeded, "done", (SideEffectCertainty) 999));
        exception.ParamName.ShouldBe("sideEffectCertainty");
    }

    [Fact]
    public void With_WhenApplied_ProducesIndependentCopy()
    {
        var original = new TaskDelegationChildResult(DelegationId(), GoalId(), AgentId(), SessionId(), AttemptId(), RunId(), TaskDelegationStatus.Succeeded, "done", SideEffectCertainty.DefinitelyPerformed);
        var copy = original with { Summary = "changed" };
        copy.Summary.ShouldBe("changed");
        original.Summary.ShouldBe("done");
    }

    private static DelegationId DelegationId() => new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static GoalId GoalId() => new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
    private static AgentId AgentId() => new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    private static SessionId SessionId() => new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
    private static GoalAttemptId AttemptId() => new(Guid.Parse("50000000-0000-0000-0000-000000000005"));
    private static RunId RunId() => new(Guid.Parse("60000000-0000-0000-0000-000000000006"));
}
