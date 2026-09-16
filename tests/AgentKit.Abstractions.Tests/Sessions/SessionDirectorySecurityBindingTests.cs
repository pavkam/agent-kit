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

    [Fact]
    public void CreationResource_WhenArgumentsAreValid_ProducesExpectedResource()
    {
        var resource = SessionDirectorySecurityBinding.CreationResource(new TenantId("tenant"), SessionsTestData.AgentId, new IdempotencyKey("retry"));
        resource.Kind.ShouldBe(ProtectedResourceKind.ApplicationState);
        resource.Identifier.ShouldStartWith($"session-directory-create:tenant/{SessionsTestData.AgentId}/sha256:");
    }

    [Fact]
    public void CreationResource_WhenTenantIdIsDefault_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => SessionDirectorySecurityBinding.CreationResource(default, SessionsTestData.AgentId, new IdempotencyKey("retry"))).ParamName.ShouldBe("tenantId");

    [Fact]
    public void CreationResource_WhenAgentIdIsDefault_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => SessionDirectorySecurityBinding.CreationResource(new TenantId("tenant"), default, new IdempotencyKey("retry"))).ParamName.ShouldBe("agentId");

    [Fact]
    public void CreationResource_WhenIdempotencyKeyIsDefault_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => SessionDirectorySecurityBinding.CreationResource(new TenantId("tenant"), SessionsTestData.AgentId, default)).ParamName.ShouldBe("idempotencyKey");

    [Fact]
    public void ListResource_WhenArgumentsAreValid_ProducesExpectedResource()
    {
        var resource = SessionDirectorySecurityBinding.ListResource(new TenantId("tenant"), SessionsTestData.AgentId);
        resource.ShouldBe(new ProtectedResource(ProtectedResourceKind.ApplicationState, $"session-directory-list:tenant/{SessionsTestData.AgentId}"));
    }

    [Fact]
    public void ListFingerprint_WhenRequestIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => SessionDirectorySecurityBinding.ListFingerprint(null!)).ParamName.ShouldBe("request");

    [Fact]
    public void ListFingerprint_WhenCalledTwiceWithSameRequest_ProducesStableDigest()
    {
        var request = new SessionDirectoryListRequest(SessionsTestData.AgentId, SessionsTestData.Identity(),
            SessionsTestData.Authorization(SessionsTestData.BeforeRun(), null), null, 10);
        SessionDirectorySecurityBinding.ListFingerprint(request).ShouldBe(SessionDirectorySecurityBinding.ListFingerprint(request));
    }

    [Fact]
    public void RecordFingerprint_WhenRequestIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => SessionDirectorySecurityBinding.RecordFingerprint(null!)).ParamName.ShouldBe("request");

    [Fact]
    public void RecordFingerprint_WhenCalledTwiceWithSameRequest_ProducesStableDigest()
    {
        var request = new SessionDirectoryWriteRequest(SessionsTestData.BeforeRunContext(), SessionsTestData.Location(), new IdempotencyKey("write"));
        SessionDirectorySecurityBinding.RecordFingerprint(request).ShouldBe(SessionDirectorySecurityBinding.RecordFingerprint(request));
    }

    [Fact]
    public void LocateForCreateFingerprint_WhenRequestIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => SessionDirectorySecurityBinding.LocateForCreateFingerprint(null!)).ParamName.ShouldBe("request");

    [Fact]
    public void LocateForCreateFingerprint_WhenCalledTwiceWithSameRequest_ProducesStableDigest()
    {
        var request = SessionsTestData.CreateRequest();
        SessionDirectorySecurityBinding.LocateForCreateFingerprint(request).ShouldBe(SessionDirectorySecurityBinding.LocateForCreateFingerprint(request));
    }

    [Fact]
    public void RecordCreateFingerprint_WhenRequestIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => SessionDirectorySecurityBinding.RecordCreateFingerprint(null!)).ParamName.ShouldBe("request");

    [Fact]
    public void RecordCreateFingerprint_WhenCalledTwiceWithSameRequest_ProducesStableDigest()
    {
        var request = new SessionDirectoryCreateRecordRequest(SessionsTestData.CreateRequest(), SessionsTestData.Location());
        SessionDirectorySecurityBinding.RecordCreateFingerprint(request).ShouldBe(SessionDirectorySecurityBinding.RecordCreateFingerprint(request));
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
