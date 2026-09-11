// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

/// <summary>Verifies SessionAppended behavior and contracts.</summary>
public sealed class SessionAppendedTests
{
    private static readonly Guid _agentGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _sessionGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid _branchGuid = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid _operationGuid = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid _runGuid = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid _entryGuid = Guid.Parse("66666666-6666-6666-6666-666666666666");
    [Fact]
    public void SessionAppended_Equality_WhenSameValues_InstancesAreEqual()
    {
        var entries = ImmutableArray.Create<SessionEntry>(MessageEntry());
        var first = new SessionAppended(new SessionVersion(1), entries);
        var second = new SessionAppended(new SessionVersion(1), entries);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    private static AgentId AgentId => new(_agentGuid);
    private static SessionId SessionId => new(_sessionGuid);
    private static BranchId BranchId => new(_branchGuid);

    private static InRunOperationCorrelation Correlation() => new(new OperationId(_operationGuid), new RunId(_runGuid), null);
    private static SessionAddress Address() => new(AgentId, SessionId);
    private static MessageSessionEntry MessageEntry() => new(new SessionEntryId(_entryGuid), Address(), Correlation(), BranchId, new SessionSequence(1), null, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), new UserMessage(new MessageId(_entryGuid), AgentId, SessionId, null, BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, [new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty));
}
