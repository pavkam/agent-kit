// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

/// <summary>Verifies SessionDeleted behavior and contracts.</summary>
public sealed class SessionDeletedTests
{
    private static readonly Guid _agentGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _sessionGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
    [Fact]
    public void SessionDeleted_Equality_WhenSameValues_InstancesAreEqual() => new SessionDeleted(Address()).ShouldBe(new SessionDeleted(Address()));

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionDeleted(Address());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static AgentId AgentId => new(_agentGuid);
    private static SessionId SessionId => new(_sessionGuid);

    private static SessionAddress Address() => new(AgentId, SessionId);
}
