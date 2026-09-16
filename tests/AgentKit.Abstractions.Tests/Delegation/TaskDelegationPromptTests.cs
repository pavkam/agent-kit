// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Delegation;



/// <summary>Verifies TaskDelegationPrompt behavior and contracts.</summary>
public sealed class TaskDelegationPromptTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var prompt = Prompt();
        prompt.Id.ShouldBe(DelegationId());
        prompt.ParentAgentId.ShouldBe(AgentId());
        prompt.ParentSessionId.ShouldBe(SessionId());
        prompt.ParentRunId.ShouldBe(RunId());
        prompt.Correlation.ShouldBe(Correlation());
        prompt.ToolCallId.ShouldBe(ToolCallId());
        prompt.Identity.ShouldBe(Identity());
        prompt.TargetAgentId.ShouldBe(TargetAgentId());
        prompt.Objective.ShouldBe("objective");
        prompt.AcceptanceCriteria.ShouldBe(["criteria"]);
        prompt.AllowedTools.ShouldBe([new ToolId("tool")]);
        prompt.Budget.ShouldBe(Budget());
        prompt.Deadline.ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void Constructor_WhenIdIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Prompt(id: default(DelegationId)));
        exception.ParamName.ShouldBe("id");
    }

    [Fact]
    public void Constructor_WhenParentAgentIdIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Prompt(parentAgentId: default(AgentId)));
        exception.ParamName.ShouldBe("parentAgentId");
    }

    [Fact]
    public void Constructor_WhenParentSessionIdIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Prompt(parentSessionId: default(SessionId)));
        exception.ParamName.ShouldBe("parentSessionId");
    }

    [Fact]
    public void Constructor_WhenParentRunIdIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Prompt(parentRunId: default(RunId)));
        exception.ParamName.ShouldBe("parentRunId");
    }

    [Fact]
    public void Constructor_WhenCorrelationIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new TaskDelegationPrompt(DelegationId(), AgentId(), SessionId(), RunId(), null!, ToolCallId(), Identity(), TargetAgentId(), "objective", ["criteria"], [new ToolId("tool")], Budget(), DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("correlation");
    }

    [Fact]
    public void Constructor_WhenToolCallIdIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Prompt(toolCallId: default(ToolCallId)));
        exception.ParamName.ShouldBe("toolCallId");
    }

    [Fact]
    public void Constructor_WhenIdentityIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new TaskDelegationPrompt(DelegationId(), AgentId(), SessionId(), RunId(), Correlation(), ToolCallId(), null!, TargetAgentId(), "objective", ["criteria"], [new ToolId("tool")], Budget(), DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("identity");
    }

    [Fact]
    public void Constructor_WhenTargetAgentIdIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Prompt(targetAgentId: default(AgentId)));
        exception.ParamName.ShouldBe("targetAgentId");
    }

    [Fact]
    public void Constructor_WhenObjectiveIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => Prompt(objective: " "));
        exception.ParamName.ShouldBe("objective");
    }

    [Fact]
    public void Constructor_WhenAcceptanceCriteriaIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => Prompt(acceptanceCriteria: []));
        exception.ParamName.ShouldBe("acceptanceCriteria");
    }

    [Fact]
    public void Constructor_WhenAcceptanceCriteriaContainsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => Prompt(acceptanceCriteria: [null!]));
        exception.ParamName.ShouldBe("acceptanceCriteria");
    }

    [Fact]
    public void Constructor_WhenAllowedToolsIsDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => Prompt(allowedTools: default(ImmutableArray<ToolId>)));
        exception.ParamName.ShouldBe("allowedTools");
    }

    [Fact]
    public void Constructor_WhenBudgetIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new TaskDelegationPrompt(DelegationId(), AgentId(), SessionId(), RunId(), Correlation(), ToolCallId(), Identity(), TargetAgentId(), "objective", ["criteria"], [new ToolId("tool")], null!, DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("budget");
    }

    [Fact]
    public void With_WhenApplied_ProducesIndependentCopy()
    {
        var original = Prompt();
        var copy = original with { Objective = "changed" };
        copy.Objective.ShouldBe("changed");
        original.Objective.ShouldBe("objective");
    }

    private static TaskDelegationPrompt Prompt(
        DelegationId? id = null,
        AgentId? parentAgentId = null,
        SessionId? parentSessionId = null,
        RunId? parentRunId = null,
        InRunOperationCorrelation? correlation = null,
        ToolCallId? toolCallId = null,
        ExecutionIdentity? identity = null,
        AgentId? targetAgentId = null,
        string objective = "objective",
        ImmutableArray<string>? acceptanceCriteria = null,
        ImmutableArray<ToolId>? allowedTools = null,
        TaskDelegationBudget? budget = null,
        DateTimeOffset? deadline = null) => new(
        id ?? DelegationId(),
        parentAgentId ?? AgentId(),
        parentSessionId ?? SessionId(),
        parentRunId ?? RunId(),
        correlation ?? Correlation(),
        toolCallId ?? ToolCallId(),
        identity ?? Identity(),
        targetAgentId ?? TargetAgentId(),
        objective,
        acceptanceCriteria ?? ["criteria"],
        allowedTools ?? [new ToolId("tool")],
        budget ?? Budget(),
        deadline ?? DateTimeOffset.UnixEpoch);

    private static DelegationId DelegationId() => new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static AgentId AgentId() => new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
    private static SessionId SessionId() => new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    private static RunId RunId() => new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
    private static OperationId OperationId() => new(Guid.Parse("50000000-0000-0000-0000-000000000005"));
    private static InRunOperationCorrelation Correlation() => new(OperationId(), RunId(), null);
    private static ToolCallId ToolCallId() => new(Guid.Parse("60000000-0000-0000-0000-000000000006"));
    private static AgentId TargetAgentId() => new(Guid.Parse("70000000-0000-0000-0000-000000000007"));
    private static TaskDelegationBudget Budget() => new(5, 10);
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
}
