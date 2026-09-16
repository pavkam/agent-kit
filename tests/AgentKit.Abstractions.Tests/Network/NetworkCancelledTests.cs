// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

/// <summary>Verifies NetworkCancelled behavior and contracts.</summary>
public sealed class NetworkCancelledTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsSideEffectCertain()
    {
        var cancelled = new NetworkCancelled(true);
        cancelled.SideEffectCertain.ShouldBeTrue();
        NetworkSendResult result = cancelled;
        _ = result.ShouldBeOfType<NetworkCancelled>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new NetworkCancelled(true);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
