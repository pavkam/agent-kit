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
    public void EmbeddingLimits_Constructor_WhenValid_RoundTripsProperties()
    {
        var limits = new EmbeddingLimits(96, 8192, 1536, 3072);
        limits.MaxInputsPerRequest.ShouldBe(96);
        limits.MaxInputTokensPerInput.ShouldBe(8192);
        limits.DefaultDimensions.ShouldBe(1536);
        limits.MaxDimensions.ShouldBe(3072);
    }
}
