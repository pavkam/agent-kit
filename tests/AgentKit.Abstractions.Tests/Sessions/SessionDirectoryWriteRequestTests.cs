// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;



/// <summary>Verifies SessionDirectoryWriteRequest behavior and contracts.</summary>
public sealed class SessionDirectoryWriteRequestTests
{
    [Fact]
    public void SessionDirectoryWriteRequest_WhenLocationTenantDiffers_ThrowsExactArgumentException()
    {
        var context = Context();
        var location = Location(tenantId: new TenantId("other"));
        var exception = Should.Throw<ArgumentException>(() => new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("record")));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("location");
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

    private static SessionLocation Location(TenantId? tenantId = null) => new(new SessionAddress(new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111")), new SessionId(Guid.Parse("22222222-2222-2222-2222-222222222222"))), tenantId ?? new TenantId("tenant"), new SessionStoreKey("store"), new SessionDirectoryRevision(1), DateTimeOffset.UnixEpoch, new SchemaVersion("v1"));
}
