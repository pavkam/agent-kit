// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

using System.Net;

using AgentKit;

/// <summary>Verifies NetworkDestinationPolicy behavior and contracts.</summary>
public sealed class NetworkDestinationPolicyTests
{
    [Theory]
    [InlineData("10.0.0.5")]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    public void AllowsAddress_WhenAddressIsPrivateOrLoopback_ReturnsFalseByDefault(string literal) =>
        NetworkDestinationPolicy.Default.AllowsAddress(IPAddress.Parse(literal)).ShouldBeFalse();

    [Theory]
    [InlineData("::ffff:10.0.0.5")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("::ffff:192.168.1.1")]
    [InlineData("::ffff:172.16.0.1")]
    [InlineData("::")]
    [InlineData("0.0.0.0")]
    [InlineData("100.64.0.1")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    public void AllowsAddress_WhenAddressReachesPrivateRangeThroughMappingOrSpecialUse_ReturnsFalseByDefault(string literal) =>
        // network-access-and-egress.md: default egress denies private, link-local, loopback, and metadata ranges regardless
        // of how the address is spelled. IPv4-mapped IPv6 connects to the embedded IPv4 on dual-stack hosts.
        NetworkDestinationPolicy.Default.AllowsAddress(IPAddress.Parse(literal)).ShouldBeFalse(literal);

    [Theory]
    [InlineData("93.184.216.34")]
    [InlineData("2606:2800:220:1:248:1893:25c8:1946")]
    public void AllowsAddress_WhenAddressIsPublic_ReturnsTrue(string literal) =>
        NetworkDestinationPolicy.Default.AllowsAddress(IPAddress.Parse(literal)).ShouldBeTrue();

    [Fact]
    public void AllowsAddress_WhenAddressIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => NetworkDestinationPolicy.Default.AllowsAddress(null!)).ParamName.ShouldBe("address");
}
