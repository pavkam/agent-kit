// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

/// <summary>Verifies NetworkResponseReceived behavior and contracts.</summary>
public sealed class NetworkResponseReceivedTests
{
    [Fact]
    public void Constructor_WhenResponseIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new NetworkResponseReceived(null!)).ParamName.ShouldBe("response");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsResponse()
    {
        var response = new NetworkTestData.FakeNetworkResponse(new NetworkResponseMetadata(200, NetworkHeaderSet.Empty, null));
        var received = new NetworkResponseReceived(response);
        received.Response.ShouldBeSameAs(response);
        NetworkSendResult result = received;
        _ = result.ShouldBeOfType<NetworkResponseReceived>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var response = new NetworkTestData.FakeNetworkResponse(new NetworkResponseMetadata(200, NetworkHeaderSet.Empty, null));
        var original = new NetworkResponseReceived(response);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
