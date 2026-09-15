// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;

/// <summary>Verifies KnownModelPricing behavior and contracts.</summary>
public sealed class KnownModelPricingTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenCurrencyIsBlank_ThrowsArgumentException(string? currency)
    {
        var exception = Should.Throw<ArgumentException>(() => new KnownModelPricing(currency!, 1m, 2m, null, null));

        exception.ParamName.ShouldBe("currency");
    }

    [Theory]
    [InlineData("inputPerMillionTokens")]
    [InlineData("outputPerMillionTokens")]
    [InlineData("cacheReadPerMillionTokens")]
    [InlineData("cacheWritePerMillionTokens")]
    public void Constructor_WhenAnAmountIsNegative_ThrowsArgumentOutOfRangeExceptionNamingThatAmount(string parameter)
    {
        decimal? Amount(string name) => name == parameter ? -0.01m : 1m;

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new KnownModelPricing(
            "USD",
            Amount("inputPerMillionTokens"),
            Amount("outputPerMillionTokens"),
            Amount("cacheReadPerMillionTokens"),
            Amount("cacheWritePerMillionTokens")));

        exception.ParamName.ShouldBe(parameter);
    }

    [Fact]
    public void Constructor_WhenAmountsAreZeroOrAbsent_Accepts()
    {
        var pricing = new KnownModelPricing("USD", 0m, null, 0m, null);

        pricing.Currency.ShouldBe("USD");
        pricing.InputPerMillionTokens.ShouldBe(0m);
        pricing.OutputPerMillionTokens.ShouldBeNull();
        pricing.CacheReadPerMillionTokens.ShouldBe(0m);
        pricing.CacheWritePerMillionTokens.ShouldBeNull();
    }

    [Fact]
    public void ToModelPricing_WhenCalled_ProjectsInputOutputAndCurrencyOnly()
    {
        var pricing = new KnownModelPricing("USD", 1.25m, 10m, 0.125m, 0m);

        var projected = pricing.ToModelPricing();

        projected.InputCostPerMillionTokens.ShouldBe(1.25m);
        projected.OutputCostPerMillionTokens.ShouldBe(10m);
        projected.CostCurrency.ShouldBe("USD");
    }

    [Fact]
    public void Equals_WhenValuesMatch_InstancesAreEqual()
    {
        new KnownModelPricing("USD", 1m, 2m, 3m, 4m).ShouldBe(new KnownModelPricing("USD", 1m, 2m, 3m, 4m));
        new KnownModelPricing("USD", 1m, 2m, 3m, 4m).ShouldNotBe(new KnownModelPricing("EUR", 1m, 2m, 3m, 4m));
    }
}
