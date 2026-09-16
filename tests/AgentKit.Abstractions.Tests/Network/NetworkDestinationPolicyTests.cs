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

    [Theory]
    [InlineData("230.5.6.7")]
    [InlineData("240.1.2.3")]
    public void AllowsAddress_WhenAddressIsInReservedTopRange_ReturnsFalse(string literal) =>
        NetworkDestinationPolicy.Default.AllowsAddress(IPAddress.Parse(literal)).ShouldBeFalse();

    [Fact]
    public void Constructor_WhenAllowedSchemesIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new NetworkDestinationPolicy(default, null, false)).ParamName.ShouldBe("allowedSchemes");

    [Fact]
    public void Constructor_WhenAllowedSchemesIsEmpty_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new NetworkDestinationPolicy([], null, false)).ParamName.ShouldBe("allowedSchemes");

    [Fact]
    public void Constructor_WhenAllowedHostsIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new NetworkDestinationPolicy(["https"], default(ImmutableArray<NormalizedHost>), false)).ParamName.ShouldBe("allowedHosts");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        ImmutableArray<NormalizedHost> hosts = [new NormalizedHost("example.test")];
        var policy = new NetworkDestinationPolicy(["HTTPS"], hosts, true);
        policy.AllowedSchemes.ShouldBe(["https"]);
        policy.AllowedHosts.ShouldBe(hosts);
        policy.AllowPrivateAddresses.ShouldBeTrue();
    }

    [Fact]
    public void AllowsSchemeAndHost_WhenDestinationIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => NetworkDestinationPolicy.Default.AllowsSchemeAndHost(null!)).ParamName.ShouldBe("destination");

    [Fact]
    public void AllowsSchemeAndHost_WhenSchemeNotAllowed_ReturnsFalse()
    {
        var destination = new NetworkDestination("ftp", new NormalizedHost("example.test"), 21, NetworkRoute.Root);
        NetworkDestinationPolicy.Default.AllowsSchemeAndHost(destination).ShouldBeFalse();
    }

    [Fact]
    public void AllowsSchemeAndHost_WhenHostRestrictedAndMatches_ReturnsTrue()
    {
        var policy = new NetworkDestinationPolicy(["https"], [new NormalizedHost("example.test")], false);
        var destination = new NetworkDestination("https", new NormalizedHost("example.test"), 443, NetworkRoute.Root);
        policy.AllowsSchemeAndHost(destination).ShouldBeTrue();
    }

    [Fact]
    public void AllowsSchemeAndHost_WhenHostRestrictedAndDoesNotMatch_ReturnsFalse()
    {
        var policy = new NetworkDestinationPolicy(["https"], [new NormalizedHost("example.test")], false);
        var destination = new NetworkDestination("https", new NormalizedHost("other.test"), 443, NetworkRoute.Root);
        policy.AllowsSchemeAndHost(destination).ShouldBeFalse();
    }

    [Fact]
    public void AllowsSchemeAndHost_WhenHostsUnrestricted_ReturnsTrue()
    {
        var destination = new NetworkDestination("https", new NormalizedHost("anything.test"), 443, NetworkRoute.Root);
        NetworkDestinationPolicy.Default.AllowsSchemeAndHost(destination).ShouldBeTrue();
    }

    [Fact]
    public void Equality_WhenAllowedHostsAreBothNull_IsEqual()
    {
        var left = new NetworkDestinationPolicy(["https"], null, false);
        var right = new NetworkDestinationPolicy(["https"], null, false);
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void Equality_WhenOneAllowedHostsIsNull_IsNotEqual()
    {
        var left = new NetworkDestinationPolicy(["https"], null, false);
        var right = new NetworkDestinationPolicy(["https"], [new NormalizedHost("example.test")], false);
        left.ShouldNotBe(right);
    }

    [Fact]
    public void Equality_WhenEquivalentHostArraysDifferByInstance_IsStructurallyEqual()
    {
        var left = new NetworkDestinationPolicy(["https"], [new NormalizedHost("example.test")], false);
        var right = new NetworkDestinationPolicy(["https"], [new NormalizedHost("example.test")], false);
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new NetworkDestinationPolicy(["https"], [new NormalizedHost("example.test")], false);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
