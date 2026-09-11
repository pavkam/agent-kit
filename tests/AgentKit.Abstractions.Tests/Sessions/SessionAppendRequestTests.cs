// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

/// <summary>Verifies SessionAppendRequest behavior and contracts.</summary>
public sealed class SessionAppendRequestTests
{
    private static readonly Guid _agentGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _sessionGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid _branchGuid = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid _operationGuid = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid _runGuid = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid _entryGuid = Guid.Parse("66666666-6666-6666-6666-666666666666");
    [Fact]
    public void SessionAppendRequest_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = AppendRequest();
        var second = AppendRequest();
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    private static AgentId AgentId => new(_agentGuid);
    private static SessionId SessionId => new(_sessionGuid);
    private static BranchId BranchId => new(_branchGuid);

    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("t"), new PrincipalId("p"), ExecutionSubjectKind.Human);
    private static InRunOperationCorrelation Correlation() => new(new OperationId(_operationGuid), new RunId(_runGuid), null);
    private static SessionAddress Address() => new(AgentId, SessionId);
    private static SessionOperationContext OperationContext() => new(AgentId, SessionId, null, Correlation(), Identity(), Authorization(Correlation(), SessionId));
    private static SecurityAuthorizationContext Authorization(OperationCorrelation correlation, SessionId? sessionId) => new(new SecurityProfileKey("security"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("77777777-7777-7777-7777-777777777777")), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(1), new ConfigurationVersion(1), new SecurityAuthorizationScope(AgentId, sessionId, correlation), Identity());
    private static MessageSessionEntry MessageEntry() => new(new SessionEntryId(_entryGuid), Address(), Correlation(), BranchId, new SessionSequence(1), null, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), new UserMessage(new MessageId(_entryGuid), AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, [new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty));
    private static SessionAppendRequest AppendRequest() => new(OperationContext(), BranchId, new SessionVersion(0), new IdempotencyKey("key"), [MessageEntry()]);
}
