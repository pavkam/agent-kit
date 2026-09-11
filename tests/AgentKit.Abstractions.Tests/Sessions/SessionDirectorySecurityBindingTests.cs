// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;



/// <summary>Verifies SessionDirectorySecurityBinding behavior and contracts.</summary>
public sealed class SessionDirectorySecurityBindingTests
{
    [Fact]
    public void SessionDirectorySecurityBinding_WhenLocationIsRequested_ProducesTenantPartitionedResourceAndStableFingerprint()
    {
        var context = Context();
        var first = SessionDirectorySecurityBinding.LocateFingerprint(context);
        var second = SessionDirectorySecurityBinding.LocateFingerprint(context);
        SessionDirectorySecurityBinding.Resource(context.Identity.TenantId, context.ToAddress()).ShouldBe(new ProtectedResource(ProtectedResourceKind.ApplicationState, "session-directory:tenant/11111111-1111-1111-1111-111111111111/22222222-2222-2222-2222-222222222222"));
        first.ShouldBe(second);
    }

    private static SessionOperationContext Context() => new(new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111")), new SessionId(Guid.Parse("22222222-2222-2222-2222-222222222222")), executionLaneId: null, new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("33333333-3333-3333-3333-333333333333")), null), TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human), Authorization());
    private static SecurityAuthorizationContext Authorization()
    {
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var agentId = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var sessionId = new SessionId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("33333333-3333-3333-3333-333333333333")), null);
        return new SecurityAuthorizationContext(new SecurityProfileKey("security"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("44444444-4444-4444-4444-444444444444")), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(1), new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);
    }
}
