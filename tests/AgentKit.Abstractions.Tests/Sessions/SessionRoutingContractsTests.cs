// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

public sealed class SessionRoutingContractsTests
{
    [Fact]
    public void SessionProfileVersion_WhenValueIsZero_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionProfileVersion(0));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void SessionProfileSnapshot_WhenAppendBoundIsZero_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Profile(maximumAppendEntries: 0));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("maximumAppendEntries");
    }

    [Fact]
    public void SessionDirectoryWriteRequest_WhenLocationTenantDiffers_ThrowsExactArgumentException()
    {
        var context = Context();
        var location = Location(tenantId: new TenantId("other"));

        var exception = Should.Throw<ArgumentException>(() => new SessionDirectoryWriteRequest(context, location, new IdempotencyKey("record")));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("location");
    }

    [Fact]
    public void ThrowIfInvalidSessionDirectoryWriteBinding_WhenLocationTenantDiffers_ThrowsExactArgumentExceptionWithInferredParameterName()
    {
        var context = Context();
        var location = Location(tenantId: new TenantId("other"));

        var exception = Should.Throw<ArgumentException>(() =>
            ArgumentException.ThrowIfInvalidSessionDirectoryWriteBinding(context, location));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe(nameof(location));
    }

    [Fact]
    public void SessionStoreDescriptor_WhenCapabilitiesContainUnknownFlag_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionStoreDescriptor(
            new SessionStoreKey("store"), (SessionStoreCapabilities) 16, SessionConsistencyModel.Strong, durable: false,
            supportsDistributedFencing: false));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("capabilities");
    }

    [Fact]
    public void SessionStoreSelectionRejected_WhenReasonIsUndefined_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionStoreSelectionRejected(
            (SessionStoreSelectionRejectionReason) 42, "safe"));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("reason");
    }

    [Fact]
    public void SessionDirectorySecurityBinding_WhenLocationIsRequested_ProducesTenantPartitionedResourceAndStableFingerprint()
    {
        var context = Context();
        var first = SessionDirectorySecurityBinding.LocateFingerprint(context);
        var second = SessionDirectorySecurityBinding.LocateFingerprint(context);

        SessionDirectorySecurityBinding.Resource(context.Identity.TenantId, context.ToAddress()).ShouldBe(
            new ProtectedResource(ProtectedResourceKind.ApplicationState,
                "session-directory:tenant/11111111-1111-1111-1111-111111111111/22222222-2222-2222-2222-222222222222"));
        first.ShouldBe(second);
    }

    [Fact]
    public void SessionDirectoryCreateRecordRequest_WhenLocationTenantDiffers_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new SessionDirectoryCreateRecordRequest(
            CreateRequest(), Location(tenantId: new TenantId("other"))));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("location");
    }

    [Fact]
    public void ThrowIfInvalidSessionDirectoryCreationBinding_WhenLocationTenantDiffers_ThrowsExactArgumentExceptionWithInferredParameterName()
    {
        var location = Location(tenantId: new TenantId("other"));

        var exception = Should.Throw<ArgumentException>(() =>
            ArgumentException.ThrowIfInvalidSessionDirectoryCreationBinding(CreateRequest(), location));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe(nameof(location));
    }

    private static SessionProfileSnapshot Profile(int maximumAppendEntries = 8) => new(
        new SessionProfileReference(new SessionProfileKey("profile"), new SessionProfileVersion(1)),
        new ComponentKey<ISessionCoordinator>("coordinator"),
        new ComponentKey<ISessionRunCoordinator>("run-coordinator"),
        new SessionStoreKey("store"),
        SessionStoreCapabilities.None,
        requiresDurableStore: false,
        requiresDistributedFencing: false,
        new SessionRetentionProfileKey("retention"),
        SessionBusyBehavior.Reject,
        maximumAppendEntries,
        16,
        verifySnapshotHashes: true,
        deleteOnDispose: false,
        new ContentHash("sha256:profile"));

    private static SessionOperationContext Context() => new(
        new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
        new SessionId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
        executionLaneId: null,
        new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("33333333-3333-3333-3333-333333333333")), null),
        TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human),
        Authorization());

    private static SessionCreateRequest CreateRequest() => new(
        new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
        TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human),
        CreationAuthorization(),
        conversationId: null,
        new IdempotencyKey("create"),
        ExtensionData.Empty);

    private static SecurityAuthorizationContext Authorization()
    {
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var agentId = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var sessionId = new SessionId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("33333333-3333-3333-3333-333333333333")), null);
        return new SecurityAuthorizationContext(new SecurityProfileKey("security"), new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("44444444-4444-4444-4444-444444444444")),
                new SecurityPolicyVersion(1), new ContentHash("sha256:policy")),
            new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(1), new ConfigurationVersion(1),
            new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);
    }

    private static SecurityAuthorizationContext CreationAuthorization()
    {
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var agentId = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("33333333-3333-3333-3333-333333333333")), null);
        return new SecurityAuthorizationContext(new SecurityProfileKey("security"), new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("44444444-4444-4444-4444-444444444444")),
                new SecurityPolicyVersion(1), new ContentHash("sha256:policy")),
            new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(1), new ConfigurationVersion(1),
            new SecurityAuthorizationScope(agentId, null, correlation), identity);
    }

    private static SessionLocation Location(TenantId? tenantId = null) => new(
        new SessionAddress(new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            new SessionId(Guid.Parse("22222222-2222-2222-2222-222222222222"))),
        tenantId ?? new TenantId("tenant"), new SessionStoreKey("store"), new SessionDirectoryRevision(1),
        DateTimeOffset.UnixEpoch, new SchemaVersion("v1"));
}
