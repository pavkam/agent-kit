// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

/// <summary>Verifies NetworkResponseLimitExceeded behavior and contracts.</summary>
public sealed class NetworkResponseLimitExceededTests
{
    [Fact]
    public void Constructor_WhenObservedBytesIsNegative_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new NetworkResponseLimitExceeded(-1, 100)).ParamName.ShouldBe("observedBytes");

    [Fact]
    public void Constructor_WhenMaximumBytesIsNegative_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new NetworkResponseLimitExceeded(100, -1)).ParamName.ShouldBe("maximumBytes");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var exceeded = new NetworkResponseLimitExceeded(200, 100);
        exceeded.ObservedBytes.ShouldBe(200);
        exceeded.MaximumBytes.ShouldBe(100);
        NetworkSendResult result = exceeded;
        _ = result.ShouldBeOfType<NetworkResponseLimitExceeded>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new NetworkResponseLimitExceeded(200, 100);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
