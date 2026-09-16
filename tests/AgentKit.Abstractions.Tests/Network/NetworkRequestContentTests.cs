// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

/// <summary>Verifies NetworkRequestContent behavior and contracts.</summary>
public sealed class NetworkRequestContentTests
{
    [Fact]
    public void Constructor_WhenContentTypeIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new NetworkRequestContent(" ", ReadOnlyMemory<byte>.Empty)).ParamName.ShouldBe("contentType");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var content = new NetworkRequestContent("text/plain", "hi"u8.ToArray());
        content.ContentType.ShouldBe("text/plain");
        content.Body.ToArray().ShouldBe("hi"u8.ToArray());
    }

    [Fact]
    public void Equality_WhenEquivalentBodiesDifferByInstance_IsStructurallyEqual()
    {
        var left = new NetworkRequestContent("text/plain", "hi"u8.ToArray());
        var right = new NetworkRequestContent("text/plain", "hi"u8.ToArray());
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new NetworkRequestContent("text/plain", "hi"u8.ToArray());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
