// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

/// <summary>Verifies SessionOperationContext behavior and contracts.</summary>
public sealed class SessionOperationContextTests
{
    private static readonly Guid _agentGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _sessionGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid _operationGuid = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid _runGuid = Guid.Parse("55555555-5555-5555-5555-555555555555");
    [Fact]
    public void SessionOperationContext_Constructor_WhenCorrelationNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionOperationContext(AgentId, SessionId, null, null!, Identity(), Authorization(Correlation(), SessionId)));
        exception.ParamName.ShouldBe("correlation");
    }

    [Fact]
    public void SessionOperationContext_Constructor_WhenIdentityNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionOperationContext(AgentId, SessionId, null, Correlation(), null!, Authorization(Correlation(), SessionId)));
        exception.ParamName.ShouldBe("identity");
    }

    [Fact]
    public void SessionOperationContext_Equality_WhenSameValues_InstancesAreEqual() => OperationContext().ShouldBe(OperationContext());
    [Fact]
    public void SessionOperationContext_ToAddress_ReturnsMatchingAddress()
    {
        var address = OperationContext().ToAddress();
        address.AgentId.ShouldBe(AgentId);
        address.SessionId.ShouldBe(SessionId);
    }

    private static AgentId AgentId => new(_agentGuid);
    private static SessionId SessionId => new(_sessionGuid);

    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("t"), new PrincipalId("p"), ExecutionSubjectKind.Human);
    private static InRunOperationCorrelation Correlation() => new(new OperationId(_operationGuid), new RunId(_runGuid), null);
    private static SessionOperationContext OperationContext() => new(AgentId, SessionId, null, Correlation(), Identity(), Authorization(Correlation(), SessionId));
    private static SecurityAuthorizationContext Authorization(OperationCorrelation correlation, SessionId? sessionId) => new(new SecurityProfileKey("security"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("77777777-7777-7777-7777-777777777777")), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(1), new ConfigurationVersion(1), new SecurityAuthorizationScope(AgentId, sessionId, correlation), Identity());
}
