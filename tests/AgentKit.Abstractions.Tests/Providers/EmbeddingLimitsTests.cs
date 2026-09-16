// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies EmbeddingLimits behavior and contracts.</summary>
public sealed class EmbeddingLimitsTests
{
    [Fact]
    public void EmbeddingLimits_Constructor_WhenMaxInputsLessThanOne_ThrowsArgumentOutOfRangeException() => _ = Should.Throw<ArgumentOutOfRangeException>(() => new EmbeddingLimits(0, null, null, null));

    [Fact]
    public void EmbeddingLimits_Constructor_WhenMaxInputTokensLessThanOne_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new EmbeddingLimits(null, 0, null, null)).ParamName.ShouldBe("maxInputTokensPerInput");

    [Fact]
    public void EmbeddingLimits_Constructor_WhenDefaultDimensionsLessThanOne_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new EmbeddingLimits(null, null, 0, null)).ParamName.ShouldBe("defaultDimensions");

    [Fact]
    public void EmbeddingLimits_Constructor_WhenMaxDimensionsLessThanOne_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new EmbeddingLimits(null, null, null, 0)).ParamName.ShouldBe("maxDimensions");

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new EmbeddingLimits(96, 8192, 1536, 3072);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void EmbeddingLimits_Constructor_WhenValid_RoundTripsProperties()
    {
        var limits = new EmbeddingLimits(96, 8192, 1536, 3072);
        limits.MaxInputsPerRequest.ShouldBe(96);
        limits.MaxInputTokensPerInput.ShouldBe(8192);
        limits.DefaultDimensions.ShouldBe(1536);
        limits.MaxDimensions.ShouldBe(3072);
    }
}
