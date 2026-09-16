// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelPricing behavior and contracts.</summary>
public sealed class ModelPricingTests
{
    [Fact]
    public void ModelPricing_Constructor_WhenInputCostNegative_ThrowsArgumentOutOfRangeException() => Should.Throw<ArgumentOutOfRangeException>(() => new ModelPricing(-1m, null, "USD"));
    [Fact]
    public void ModelPricing_Constructor_WhenOutputCostNegative_ThrowsArgumentOutOfRangeException() => Should.Throw<ArgumentOutOfRangeException>(() => new ModelPricing(null, -1m, "USD"));
    [Fact]
    public void ModelPricing_Constructor_WhenValid_RoundTripsProperties()
    {
        var pricing = new ModelPricing(1.5m, 2.5m, "USD");
        pricing.InputCostPerMillionTokens.ShouldBe(1.5m);
        pricing.OutputCostPerMillionTokens.ShouldBe(2.5m);
        pricing.CostCurrency.ShouldBe("USD");
    }

    [Fact]
    public void ModelPricing_Equality_WhenSameValues_InstancesAreEqual() => new ModelPricing(1m, 2m, "USD").ShouldBe(new ModelPricing(1m, 2m, "USD"));

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ModelPricing(1m, 2m, "USD");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
