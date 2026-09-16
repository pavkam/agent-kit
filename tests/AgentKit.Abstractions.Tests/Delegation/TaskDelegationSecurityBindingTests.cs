// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Delegation;



/// <summary>Verifies TaskDelegationSecurityBinding behavior and contracts.</summary>
public sealed class TaskDelegationSecurityBindingTests
{
    [Fact]
    public void Resource_WhenCalled_ProducesDelegationResource()
    {
        var id = DelegationId();
        var resource = TaskDelegationSecurityBinding.Resource(id);
        resource.Kind.ShouldBe(ProtectedResourceKind.Delegation);
        resource.Identifier.ShouldBe($"delegation:{id}");
    }

    [Fact]
    public void Fingerprint_WhenPromptIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => TaskDelegationSecurityBinding.Fingerprint(null!));
        exception.ParamName.ShouldBe("prompt");
    }

    [Fact]
    public void Fingerprint_WhenPromptsAreEquivalent_IsStable() =>
        TaskDelegationSecurityBinding.Fingerprint(Prompt()).ShouldBe(TaskDelegationSecurityBinding.Fingerprint(Prompt()));

    [Fact]
    public void Fingerprint_WhenObjectiveDiffers_ChangesEvidence()
    {
        var baseline = TaskDelegationSecurityBinding.Fingerprint(Prompt());
        var mutated = TaskDelegationSecurityBinding.Fingerprint(Prompt() with { Objective = "different" });
        mutated.ShouldNotBe(baseline);
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
}
