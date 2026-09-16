// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

/// <summary>Verifies NetworkBounds behavior and contracts.</summary>
public sealed class NetworkBoundsTests
{
    [Fact]
    public void Constructor_WhenConnectTimeoutIsNotPositive_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new NetworkBounds(TimeSpan.Zero, TimeSpan.FromSeconds(1), 1, 1)).ParamName.ShouldBe("connectTimeout");

    [Fact]
    public void Constructor_WhenResponseTimeoutIsNotPositive_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new NetworkBounds(TimeSpan.FromSeconds(1), TimeSpan.Zero, 1, 1)).ParamName.ShouldBe("responseTimeout");

    [Fact]
    public void Constructor_WhenMaximumResponseBytesIsNotPositive_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new NetworkBounds(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), 0, 1)).ParamName.ShouldBe("maximumResponseBytes");

    [Fact]
    public void Constructor_WhenMaximumRedirectsIsNegative_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new NetworkBounds(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), 1, -1)).ParamName.ShouldBe("maximumRedirects");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var bounds = new NetworkBounds(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), 4_096, 3);
        bounds.ConnectTimeout.ShouldBe(TimeSpan.FromSeconds(1));
        bounds.ResponseTimeout.ShouldBe(TimeSpan.FromSeconds(2));
        bounds.MaximumResponseBytes.ShouldBe(4_096);
        bounds.MaximumRedirects.ShouldBe(3);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new NetworkBounds(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), 4_096, 3);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
