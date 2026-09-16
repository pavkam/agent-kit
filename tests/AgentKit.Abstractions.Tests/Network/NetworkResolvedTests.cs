// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

/// <summary>Verifies NetworkResolved behavior and contracts.</summary>
public sealed class NetworkResolvedTests
{
    [Fact]
    public void Constructor_WhenAddressesIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new NetworkResolved(default)).ParamName.ShouldBe("addresses");

    [Fact]
    public void Constructor_WhenAddressesIsEmpty_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new NetworkResolved([])).ParamName.ShouldBe("addresses");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsAddresses()
    {
        var address = NetworkTestData.Address();
        var resolved = new NetworkResolved([address]);
        resolved.Addresses.ShouldBe([address]);
        NetworkResolutionResult result = resolved;
        _ = result.ShouldBeOfType<NetworkResolved>();
    }

    [Fact]
    public void Equality_WhenEquivalentArraysDifferByInstance_IsStructurallyEqual()
    {
        var left = new NetworkResolved([NetworkTestData.Address()]);
        var right = new NetworkResolved([NetworkTestData.Address()]);
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new NetworkResolved([NetworkTestData.Address()]);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
