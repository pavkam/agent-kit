// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

/// <summary>Verifies SessionCreateRequest behavior and contracts.</summary>
public sealed class SessionCreateRequestTests
{
    private static readonly Guid _agentGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _operationGuid = Guid.Parse("44444444-4444-4444-4444-444444444444");
    [Fact]
    public void SessionCreateRequest_Equality_WhenSameValues_InstancesAreEqual() => CreateRequest().ShouldBe(CreateRequest());
    private static AgentId AgentId => new(_agentGuid);

    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("t"), new PrincipalId("p"), ExecutionSubjectKind.Human);
    private static BeforeRunOperationCorrelation CreationCorrelation() => new(new OperationId(_operationGuid), null);
    private static SessionCreateRequest CreateRequest() => new(AgentId, Identity(), Authorization(CreationCorrelation(), null), null, new IdempotencyKey("key"), ExtensionData.Empty);
    private static SecurityAuthorizationContext Authorization(OperationCorrelation correlation, SessionId? sessionId) => new(new SecurityProfileKey("security"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("77777777-7777-7777-7777-777777777777")), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(1), new ConfigurationVersion(1), new SecurityAuthorizationScope(AgentId, sessionId, correlation), Identity());
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SessionCreateRequest_WhenAuthorizationIsNotSessionlessBeforeRun_ThrowsExactArgumentExceptionForAuthorization(int invalidCase)
    {
        var authorization = InvalidAuthorization(invalidCase);
        var exception = Should.Throw<ArgumentException>(() => Request(authorization));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void SessionCreateRequest_WhenAuthorizationIsNull_ThrowsExactArgumentNullExceptionForAuthorization()
    {
        var exception = Should.Throw<ArgumentNullException>(() => Request(null!));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("authorization");
    }

    private static SessionCreateRequest Request(SecurityAuthorizationContext? authorization)
    {
        var agentId = authorization?.Scope.AgentId ?? new AgentId(Guid.NewGuid());
        var identity = authorization?.Identity ?? TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        return new SessionCreateRequest(agentId, identity, authorization!, null, new IdempotencyKey("create"), ExtensionData.Empty);
    }

    private static SecurityAuthorizationContext InvalidAuthorization(int invalidCase) => invalidCase switch
    {
        0 => AuthorizationSessionCreationAuthorizationGuard(new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null), new SessionId(Guid.NewGuid())),
        1 => AuthorizationSessionCreationAuthorizationGuard(new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null), sessionId: null),
        _ => AuthorizationSessionCreationAuthorizationGuard(new AfterRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid())), sessionId: null),
    };
    private static SecurityAuthorizationContext AuthorizationSessionCreationAuthorizationGuard(OperationCorrelation correlation, SessionId? sessionId)
    {
        var agentId = new AgentId(Guid.NewGuid());
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        return new SecurityAuthorizationContext(new SecurityProfileKey("default"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.NewGuid()), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(1), new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);
    }
}
