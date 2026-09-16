// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

/// <summary>Verifies NetworkRoute behavior and contracts.</summary>
public sealed class NetworkRouteTests
{
    [Fact]
    public void Constructor_WhenValueIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new NetworkRoute(null!)).ParamName.ShouldBe("value");

    [Fact]
    public void Constructor_WhenValueIsNotRooted_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new NetworkRoute("relative")).ParamName.ShouldBe("value");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsValue()
    {
        var route = new NetworkRoute("/path");
        route.Value.ShouldBe("/path");
    }

    [Fact]
    public void Root_WhenAccessed_IsRootPath() =>
        NetworkRoute.Root.Value.ShouldBe("/");

    [Fact]
    public void ToString_WhenCalled_ReturnsValue() =>
        new NetworkRoute("/path").ToString().ShouldBe("/path");
}
