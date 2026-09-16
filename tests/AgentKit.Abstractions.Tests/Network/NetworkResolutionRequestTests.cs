// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

/// <summary>Verifies NetworkResolutionRequest behavior and contracts.</summary>
public sealed class NetworkResolutionRequestTests
{
    [Fact]
    public void Constructor_WhenDestinationIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new NetworkResolutionRequest(NetworkTestData.Id(), null!, NetworkTestData.Bounds(), NetworkTestData.Grant())).ParamName.ShouldBe("destination");

    [Fact]
    public void Constructor_WhenBoundsIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new NetworkResolutionRequest(NetworkTestData.Id(), NetworkTestData.Destination(), null!, NetworkTestData.Grant())).ParamName.ShouldBe("bounds");

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new NetworkResolutionRequest(NetworkTestData.Id(), NetworkTestData.Destination(), NetworkTestData.Bounds(), null!)).ParamName.ShouldBe("grant");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var id = NetworkTestData.Id();
        var destination = NetworkTestData.Destination();
        var bounds = NetworkTestData.Bounds();
        var grant = NetworkTestData.Grant();
        var request = new NetworkResolutionRequest(id, destination, bounds, grant);
        request.Id.ShouldBe(id);
        request.Destination.ShouldBeSameAs(destination);
        request.Bounds.ShouldBeSameAs(bounds);
        request.Grant.ShouldBeSameAs(grant);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = NetworkTestData.ResolutionRequest();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
