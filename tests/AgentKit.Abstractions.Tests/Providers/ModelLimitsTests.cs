// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelLimits behavior and contracts.</summary>
public sealed class ModelLimitsTests
{
    [Fact]
    public void ModelLimits_Constructor_WhenContextTokensNegative_ThrowsArgumentOutOfRangeException() => Should.Throw<ArgumentOutOfRangeException>(() => new ModelLimits(-1, null));
    [Fact]
    public void ModelLimits_Constructor_WhenOutputTokensNegative_ThrowsArgumentOutOfRangeException() => Should.Throw<ArgumentOutOfRangeException>(() => new ModelLimits(null, -1));
    [Fact]
    public void ModelLimits_Constructor_WhenValid_RoundTripsProperties()
    {
        var limits = new ModelLimits(1000, 500);
        limits.MaxContextTokens.ShouldBe(1000);
        limits.MaxOutputTokens.ShouldBe(500);
    }

    [Fact]
    public void ModelLimits_Equality_WhenSameValues_InstancesAreEqual() => new ModelLimits(1000, 500).ShouldBe(new ModelLimits(1000, 500));
}
