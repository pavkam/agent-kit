// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Planning;

using AgentKit.TestSupport;

/// <summary>Verifies PlanStatusRequest behavior and contracts.</summary>
public sealed class PlanStatusRequestTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var context = Context();
        var profile = TestSecurityEvidence.SessionProfile();
        var toolCallId = ToolCallId();
        var itemId = new PlanItemId("one");
        var grant = Grant();
        var request = new PlanStatusRequest(context, profile, toolCallId, itemId, PlanItemStatus.Completed, new PlanRevision(1), grant);
        request.Context.ShouldBe(context);
        request.SessionProfile.ShouldBe(profile);
        request.ToolCallId.ShouldBe(toolCallId);
        request.ItemId.ShouldBe(itemId);
        request.Status.ShouldBe(PlanItemStatus.Completed);
        request.ExpectedRevision.ShouldBe(new PlanRevision(1));
        request.Grant.ShouldBe(grant);
    }

    [Fact]
    public void Constructor_WhenContextIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new PlanStatusRequest(null!, TestSecurityEvidence.SessionProfile(), ToolCallId(), new PlanItemId("one"), PlanItemStatus.Completed, new PlanRevision(1), Grant()));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenSessionProfileIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new PlanStatusRequest(Context(), null!, ToolCallId(), new PlanItemId("one"), PlanItemStatus.Completed, new PlanRevision(1), Grant()));
        exception.ParamName.ShouldBe("sessionProfile");
    }

    [Fact]
    public void Constructor_WhenStatusIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new PlanStatusRequest(Context(), TestSecurityEvidence.SessionProfile(), ToolCallId(), new PlanItemId("one"), (PlanItemStatus) 999, new PlanRevision(1), Grant()));
        exception.ParamName.ShouldBe("status");
    }

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new PlanStatusRequest(Context(), TestSecurityEvidence.SessionProfile(), ToolCallId(), new PlanItemId("one"), PlanItemStatus.Completed, new PlanRevision(1), null!));
        exception.ParamName.ShouldBe("grant");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new PlanStatusRequest(Context(), TestSecurityEvidence.SessionProfile(), ToolCallId(), new PlanItemId("one"), PlanItemStatus.Completed, new PlanRevision(1), Grant());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static AgentId AgentId() => new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static SessionId SessionId() => new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
    private static OperationId OperationId() => new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    private static RunId RunId() => new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
    private static InRunOperationCorrelation Correlation() => new(OperationId(), RunId(), null);
    private static ToolCallId ToolCallId() => new(Guid.Parse("50000000-0000-0000-0000-000000000005"));
    private static ExecutionIdentity Identity() => TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
    private static SessionOperationContext Context() => new(AgentId(), SessionId(), null, Correlation(), Identity(), TestSecurityEvidence.Authorization(AgentId(), SessionId(), Correlation(), Identity()));
    private static SecurityGrant Grant() => new(
        new GrantId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
        new SecurityRequestId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
        new SecurityAuthorizationScope(AgentId(), SessionId(), Correlation()),
        Identity(),
        new ComponentId("plan-store"),
        SecurityOperationKind.StateMutation,
        SecurityEffect.Mutate,
        [new ProtectedResource(ProtectedResourceKind.ApplicationState, "plan")],
        new InputFingerprint("sha256:test"),
        new SecurityPolicyVersion(1),
        new SecurityRevocationVersion(1),
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch.AddMinutes(1),
        1);
}
