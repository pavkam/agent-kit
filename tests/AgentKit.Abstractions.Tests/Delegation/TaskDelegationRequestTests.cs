// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Delegation;



/// <summary>Verifies TaskDelegationRequest behavior and contracts.</summary>
public sealed class TaskDelegationRequestTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var prompt = Prompt();
        var grant = Grant();
        var request = new TaskDelegationRequest(prompt, grant);
        request.Prompt.ShouldBe(prompt);
        request.Grant.ShouldBe(grant);
    }

    [Fact]
    public void Constructor_WhenPromptIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new TaskDelegationRequest(null!, Grant()));
        exception.ParamName.ShouldBe("prompt");
    }

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new TaskDelegationRequest(Prompt(), null!));
        exception.ParamName.ShouldBe("grant");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new TaskDelegationRequest(Prompt(), Grant());
        var copy = original with { };
        copy.ShouldBe(original);
    }

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
    private static TaskDelegationPrompt Prompt() => new(DelegationId(), AgentId(), SessionId(), RunId(), Correlation(), ToolCallId(), Identity(), TargetAgentId(), "objective", ["criteria"], [new ToolId("tool")], Budget(), DateTimeOffset.UnixEpoch);
    private static SecurityGrant Grant() => new(
        new GrantId(Guid.Parse("80000000-0000-0000-0000-000000000008")),
        new SecurityRequestId(Guid.Parse("90000000-0000-0000-0000-000000000009")),
        new SecurityAuthorizationScope(AgentId(), SessionId(), Correlation()),
        Identity(),
        new ComponentId("test"),
        SecurityOperationKind.Delegation,
        SecurityEffect.Delegate,
        [TaskDelegationSecurityBinding.Resource(DelegationId())],
        new InputFingerprint("sha256:test"),
        new SecurityPolicyVersion(1),
        new SecurityRevocationVersion(1),
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch.AddMinutes(1),
        1);
}
