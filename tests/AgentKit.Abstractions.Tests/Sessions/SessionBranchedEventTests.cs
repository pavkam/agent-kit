// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

/// <summary>Verifies SessionBranchedEvent behavior and contracts.</summary>
public sealed class SessionBranchedEventTests
{
    private static readonly Guid _agentGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _sessionGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid _branchGuid = Guid.Parse("33333333-3333-3333-3333-333333333333");
    [Fact]
    public void SessionBranchedEvent_Equality_WhenSameValues_InstancesAreEqual() => new SessionBranchedEvent(Address(), DateTimeOffset.UnixEpoch, BranchId, BranchId, new SessionSequence(1)).ShouldBe(new SessionBranchedEvent(Address(), DateTimeOffset.UnixEpoch, BranchId, BranchId, new SessionSequence(1)));
    private static AgentId AgentId => new(_agentGuid);
    private static SessionId SessionId => new(_sessionGuid);
    private static BranchId BranchId => new(_branchGuid);

    private static SessionAddress Address() => new(AgentId, SessionId);
}
