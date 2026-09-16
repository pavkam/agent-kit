// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

/// <summary>Verifies <see cref="SessionReadSnapshot"/> coordinates.</summary>
public sealed class SessionReadSnapshotTests
{
    [Fact]
    public void Constructor_WhenCoordinatesAreValid_PreservesExactPrefix()
    {
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var branch = new BranchId(Guid.NewGuid());
        var snapshot = new SessionReadSnapshot(address, branch, new SessionVersion(3), new SessionSequence(8));
        snapshot.Address.ShouldBeSameAs(address);
        snapshot.BranchId.ShouldBe(branch);
        snapshot.Version.ShouldBe(new SessionVersion(3));
        snapshot.UpperSequence.ShouldBe(new SessionSequence(8));
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionReadSnapshot(new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid())), new BranchId(Guid.NewGuid()), new SessionVersion(3), new SessionSequence(8));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
