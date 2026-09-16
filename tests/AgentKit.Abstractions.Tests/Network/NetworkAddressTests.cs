// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

using System.Net;

/// <summary>Verifies NetworkAddress behavior and contracts.</summary>
public sealed class NetworkAddressTests
{
    [Fact]
    public void Constructor_WhenAddressIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new NetworkAddress(null!, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1))).ParamName.ShouldBe("address");

    [Fact]
    public void Constructor_WhenExpiresAtIsNotLaterThanResolvedAt_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new NetworkAddress(IPAddress.Parse("192.0.2.1"), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("expiresAt");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var address = IPAddress.Parse("192.0.2.1");
        var resolvedAt = DateTimeOffset.UnixEpoch;
        var expiresAt = resolvedAt.AddMinutes(1);
        var networkAddress = new NetworkAddress(address, resolvedAt, expiresAt);
        networkAddress.Address.ShouldBe(address);
        networkAddress.ResolvedAt.ShouldBe(resolvedAt);
        networkAddress.ExpiresAt.ShouldBe(expiresAt);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new NetworkAddress(IPAddress.Parse("192.0.2.1"), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
