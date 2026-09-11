// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

/// <summary>Verifies SessionNotFound behavior and contracts.</summary>
public sealed class SessionNotFoundTests
{
    private static readonly Guid _agentGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _sessionGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
    [Fact]
    public void SessionNotFound_Equality_WhenSameValues_InstancesAreEqual() => new SessionNotFound(Address()).ShouldBe(new SessionNotFound(Address()));
    private static AgentId AgentId => new(_agentGuid);
    private static SessionId SessionId => new(_sessionGuid);

    private static SessionAddress Address() => new(AgentId, SessionId);
}
