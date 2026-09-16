// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

/// <summary>Verifies NetworkRedirectReceived behavior and contracts.</summary>
public sealed class NetworkRedirectReceivedTests
{
    [Fact]
    public void Constructor_WhenDestinationIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new NetworkRedirectReceived(null!, false)).ParamName.ShouldBe("destination");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var destination = NetworkTestData.Destination();
        var received = new NetworkRedirectReceived(destination, true);
        received.Destination.ShouldBeSameAs(destination);
        received.CrossOrigin.ShouldBeTrue();
        NetworkSendResult result = received;
        _ = result.ShouldBeOfType<NetworkRedirectReceived>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new NetworkRedirectReceived(NetworkTestData.Destination(), true);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
