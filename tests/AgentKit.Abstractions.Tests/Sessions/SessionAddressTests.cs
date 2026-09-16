// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

/// <summary>Verifies SessionAddress behavior and contracts.</summary>
public sealed class SessionAddressTests
{
    private static readonly Guid _agentGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _sessionGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
    [Fact]
    public void SessionAddress_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = new SessionAddress(AgentId, SessionId);
        var second = new SessionAddress(AgentId, SessionId);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void SessionAddress_Constructor_RoundTripsProperties()
    {
        var address = new SessionAddress(AgentId, SessionId);
        address.AgentId.ShouldBe(AgentId);
        address.SessionId.ShouldBe(SessionId);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionAddress(AgentId, SessionId);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static AgentId AgentId => new(_agentGuid);
    private static SessionId SessionId => new(_sessionGuid);
}
