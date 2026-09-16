// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

/// <summary>Verifies SessionReadRequest behavior and contracts.</summary>
public sealed class SessionReadRequestTests
{
    private static readonly Guid _agentGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _sessionGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid _branchGuid = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid _operationGuid = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid _runGuid = Guid.Parse("55555555-5555-5555-5555-555555555555");
    [Fact]
    public void SessionReadRequest_Equality_WhenSameValues_InstancesAreEqual() => ReadRequest().ShouldBe(ReadRequest());

    [Fact]
    public void Constructor_WhenSnapshotMatches_PreservesExactContinuationBoundary()
    {
        var snapshot = new SessionReadSnapshot(new SessionAddress(AgentId, SessionId), BranchId, new SessionVersion(2), new SessionSequence(8));
        var request = new SessionReadRequest(OperationContext(), BranchId, new SessionSequence(4), 10, snapshot);
        request.Snapshot.ShouldBeSameAs(snapshot);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = ReadRequest();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var context = OperationContext();
        var request = new SessionReadRequest(context, BranchId, new SessionSequence(0), 10);
        request.Context.ShouldBe(context);
        request.BranchId.ShouldBe(BranchId);
        request.FromSequenceExclusive.ShouldBe(new SessionSequence(0));
        request.PageSize.ShouldBe(10);
        request.Snapshot.ShouldBeNull();
    }

    private static AgentId AgentId => new(_agentGuid);
    private static SessionId SessionId => new(_sessionGuid);
    private static BranchId BranchId => new(_branchGuid);

    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("t"), new PrincipalId("p"), ExecutionSubjectKind.Human);
    private static InRunOperationCorrelation Correlation() => new(new OperationId(_operationGuid), new RunId(_runGuid), null);
    private static SessionOperationContext OperationContext() => new(AgentId, SessionId, null, Correlation(), Identity(), Authorization(Correlation(), SessionId));
    private static SecurityAuthorizationContext Authorization(OperationCorrelation correlation, SessionId? sessionId) => new(new SecurityProfileKey("security"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("77777777-7777-7777-7777-777777777777")), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(1), new ConfigurationVersion(1), new SecurityAuthorizationScope(AgentId, sessionId, correlation), Identity());
    private static SessionReadRequest ReadRequest() => new(OperationContext(), BranchId, new SessionSequence(0), 10);
}
