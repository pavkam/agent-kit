// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

using System.Net;

using AgentKit;

public sealed class NetworkDestinationPolicyTests
{
    [Fact]
    public void Constructor_WhenAllowedSchemesIsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new NetworkDestinationPolicy(default, null, allowPrivateAddresses: false));

        exception.ParamName.ShouldBe("allowedSchemes");
    }

    [Fact]
    public void Constructor_WhenAllowedSchemesIsEmpty_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new NetworkDestinationPolicy([], null, allowPrivateAddresses: false));

        exception.ParamName.ShouldBe("allowedSchemes");
    }

    [Fact]
    public void Constructor_WhenAllowedHostsIsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new NetworkDestinationPolicy(["https"], default(ImmutableArray<NormalizedHost>), allowPrivateAddresses: false));

        exception.ParamName.ShouldBe("allowedHosts");
    }

    [Fact]
    public void Constructor_CanonicalizesSchemesToLowercase()
    {
        var policy = new NetworkDestinationPolicy(["HTTPS"], null, allowPrivateAddresses: false);

        policy.AllowedSchemes.ShouldBe(["https"]);
    }

    [Fact]
    public void AllowsSchemeAndHost_WhenDestinationIsNull_ThrowsArgumentNullException()
    {
        var policy = NetworkDestinationPolicy.Default;

        var exception = Should.Throw<ArgumentNullException>(() => policy.AllowsSchemeAndHost(null!));

        exception.ParamName.ShouldBe("destination");
    }

    [Fact]
    public void AllowsSchemeAndHost_WhenSchemeNotAllowed_ReturnsFalse()
    {
        var policy = new NetworkDestinationPolicy(["https"], null, allowPrivateAddresses: false);
        var destination = new NetworkDestination("http", new NormalizedHost("example.com"), 80, NetworkRoute.Root);

        policy.AllowsSchemeAndHost(destination).ShouldBeFalse();
    }

    [Fact]
    public void AllowsSchemeAndHost_WhenHostsUnrestricted_AllowsAnyHost()
    {
        var policy = new NetworkDestinationPolicy(["https"], null, allowPrivateAddresses: false);
        var destination = new NetworkDestination("https", new NormalizedHost("anything.example.com"), 443, NetworkRoute.Root);

        policy.AllowsSchemeAndHost(destination).ShouldBeTrue();
    }

    [Fact]
    public void AllowsSchemeAndHost_WhenHostNotInAllowList_ReturnsFalse()
    {
        var policy = new NetworkDestinationPolicy(["https"], [new NormalizedHost("allowed.example.com")], allowPrivateAddresses: false);
        var destination = new NetworkDestination("https", new NormalizedHost("other.example.com"), 443, NetworkRoute.Root);

        policy.AllowsSchemeAndHost(destination).ShouldBeFalse();
    }

    [Fact]
    public void AllowsSchemeAndHost_WhenHostInAllowList_ReturnsTrue()
    {
        var policy = new NetworkDestinationPolicy(["https"], [new NormalizedHost("allowed.example.com")], allowPrivateAddresses: false);
        var destination = new NetworkDestination("https", new NormalizedHost("allowed.example.com"), 443, NetworkRoute.Root);

        policy.AllowsSchemeAndHost(destination).ShouldBeTrue();
    }

    [Fact]
    public void AllowsAddress_WhenAddressIsNull_ThrowsArgumentNullException()
    {
        var policy = NetworkDestinationPolicy.Default;

        var exception = Should.Throw<ArgumentNullException>(() => policy.AllowsAddress(null!));

        exception.ParamName.ShouldBe("address");
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.5")]
    [InlineData("172.16.4.4")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.1.1")]
    [InlineData("::1")]
    public void AllowsAddress_WhenAddressIsPrivateOrLoopbackAndPolicyDisallows_ReturnsFalse(string address)
    {
        var policy = new NetworkDestinationPolicy(["https"], null, allowPrivateAddresses: false);

        policy.AllowsAddress(IPAddress.Parse(address)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("93.184.216.34")]
    [InlineData("8.8.8.8")]
    public void AllowsAddress_WhenAddressIsPublic_ReturnsTrue(string address)
    {
        var policy = new NetworkDestinationPolicy(["https"], null, allowPrivateAddresses: false);

        policy.AllowsAddress(IPAddress.Parse(address)).ShouldBeTrue();
    }

    [Fact]
    public void AllowsAddress_WhenPolicyAllowsPrivateAddresses_ReturnsTrueForLoopback()
    {
        var policy = new NetworkDestinationPolicy(["https"], null, allowPrivateAddresses: true);

        policy.AllowsAddress(IPAddress.Loopback).ShouldBeTrue();
    }

    [Fact]
    public void Equals_WhenAllowedHostsBothNull_AreEqual()
    {
        var first = new NetworkDestinationPolicy(["https"], null, allowPrivateAddresses: false);
        var second = new NetworkDestinationPolicy(["https"], null, allowPrivateAddresses: false);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenAllowedHostsDiffer_AreNotEqual()
    {
        var first = new NetworkDestinationPolicy(["https"], [new NormalizedHost("a.example.com")], allowPrivateAddresses: false);
        var second = new NetworkDestinationPolicy(["https"], [new NormalizedHost("b.example.com")], allowPrivateAddresses: false);

        first.ShouldNotBe(second);
    }

    [Fact]
    public void Equals_WhenOneAllowedHostsIsNullAndOtherIsNot_AreNotEqual()
    {
        var first = new NetworkDestinationPolicy(["https"], null, allowPrivateAddresses: false);
        var second = new NetworkDestinationPolicy(["https"], [new NormalizedHost("a.example.com")], allowPrivateAddresses: false);

        first.ShouldNotBe(second);
    }
}
