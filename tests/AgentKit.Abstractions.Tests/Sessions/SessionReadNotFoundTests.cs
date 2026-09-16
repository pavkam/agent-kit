// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

/// <summary>Verifies SessionReadNotFound behavior and contracts.</summary>
public sealed class SessionReadNotFoundTests
{
    private static readonly Guid _agentGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _sessionGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
    [Fact]
    public void SessionReadNotFound_Equality_WhenSameValues_InstancesAreEqual() => new SessionReadNotFound(Address()).ShouldBe(new SessionReadNotFound(Address()));

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionReadNotFound(Address());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static AgentId AgentId => new(_agentGuid);
    private static SessionId SessionId => new(_sessionGuid);

    private static SessionAddress Address() => new(AgentId, SessionId);
}
