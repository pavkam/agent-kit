// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

using AgentKit;

public sealed class NetworkDestinationTests
{
    [Theory]
    [InlineData("bad host")]
    [InlineData("fe80::1%4")]
    [InlineData("/")]
    public void NormalizedHost_WhenHostInvalid_ThrowsWithExactParameter(string value)
    {
        Action action = () =>
        {
            _ = new NormalizedHost(value);
        };

        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe("value");
    }

    [Fact]
    public void NormalizedHost_WhenUnicodeAndTrailingDot_CanonicalizesToAsciiDnsIdentity()
    {
        var host = new NormalizedHost("BÜCHER.example.");

        host.Value.ShouldBe("xn--bcher-kva.example");
    }

    [Fact]
    public void ToString_WhenIpv6Literal_UsesUnambiguousBracketedAuthority()
    {
        var destination = new NetworkDestination(
            "https", new NormalizedHost("2001:0DB8::1"), 443, NetworkRoute.Root);

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
}
