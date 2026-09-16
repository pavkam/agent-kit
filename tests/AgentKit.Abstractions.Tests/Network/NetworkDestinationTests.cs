// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

using AgentKit;

/// <summary>Verifies NetworkDestination behavior and contracts.</summary>
public sealed class NetworkDestinationTests
{
    [Fact]
    public void ToString_WhenIpv6Literal_UsesUnambiguousBracketedAuthority()
    {
        var destination = new NetworkDestination("https", new NormalizedHost("2001:0DB8::1"), 443, NetworkRoute.Root);
        destination.ToString().ShouldBe("https://[2001:db8::1]:443/");
    }

    [Fact]
    public void Constructor_WhenHostDefault_ThrowsBeforeConstruction()
    {
        var action = () => new NetworkDestination("https", default, 443, NetworkRoute.Root);
        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe("host");
    }

    [Fact]
    public void Constructor_WhenRouteDefault_ThrowsBeforeConstruction()
    {
        var action = () => new NetworkDestination("https", new NormalizedHost("example.test"), 443, default);
        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe("route");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new NetworkDestination("https", new NormalizedHost("example.test"), 443, NetworkRoute.Root);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
