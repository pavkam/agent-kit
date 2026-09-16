// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

/// <summary>Verifies NetworkResponseMetadata behavior and contracts.</summary>
public sealed class NetworkResponseMetadataTests
{
    [Fact]
    public void Constructor_WhenHeadersIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new NetworkResponseMetadata(200, null!, null)).ParamName.ShouldBe("headers");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var headers = NetworkHeaderSet.Empty;
        var metadata = new NetworkResponseMetadata(200, headers, 1024);
        metadata.StatusCode.ShouldBe(200);
        metadata.Headers.ShouldBeSameAs(headers);
        metadata.ContentLength.ShouldBe(1024);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new NetworkResponseMetadata(200, NetworkHeaderSet.Empty, 1024);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
