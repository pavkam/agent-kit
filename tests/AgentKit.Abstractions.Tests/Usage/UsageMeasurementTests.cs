// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Usage;
/// <summary>Verifies UsageMeasurement behavior and contracts.</summary>
public sealed class UsageMeasurementTests
{
    [Theory]
    [InlineData("dimension")]
    [InlineData("unit")]
    [InlineData("quality")]
    public void Constructor_WhenMeasurementIdentityOrQualityIsInvalid_RejectsExactArgument(string parameter)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new UsageMeasurement(parameter == "dimension" ? default : BudgetDimensions.InputTokens, parameter == "unit" ? default : RunUsageTests.Tokens, null, parameter == "quality" ? (UsageMeasurementQuality) (-1) : UsageMeasurementQuality.Unknown));
        exception.ParamName.ShouldBe(parameter);
    }

    [Theory]
    [InlineData(UsageMeasurementQuality.Measured, false)]
    [InlineData(UsageMeasurementQuality.ProviderReported, false)]
    [InlineData(UsageMeasurementQuality.Estimated, false)]
    [InlineData(UsageMeasurementQuality.Unknown, true)]
    [InlineData(UsageMeasurementQuality.NotApplicable, true)]
    public void Constructor_WhenAmountPresenceConflictsWithQuality_RejectsExactArgument(UsageMeasurementQuality quality, bool hasAmount) => Should.Throw<ArgumentException>(() => new UsageMeasurement(BudgetDimensions.InputTokens, RunUsageTests.Tokens, hasAmount ? default(BudgetQuantity) : null, quality)).ParamName.ShouldBe("amount");
    [Fact]
    public void Constructor_WhenEstimatedCostHasNoPricing_RejectsInsteadOfInventingProvenance() => Should.Throw<ArgumentNullException>(() => new UsageMeasurement(BudgetDimensions.Cost, new BudgetUnit("usd"), default(BudgetQuantity), UsageMeasurementQuality.Estimated)).ParamName.ShouldBe("pricing");
    [Fact]
    public void Constructor_WhenNonCostMeasurementCarriesPricing_RejectsExactArgument() => Should.Throw<ArgumentException>(() => new UsageMeasurement(BudgetDimensions.InputTokens, RunUsageTests.Tokens, default(BudgetQuantity), UsageMeasurementQuality.Measured, new("catalog", "v1"))).ParamName.ShouldBe("pricing");
    [Theory]
    [InlineData("default")]
    [InlineData("null")]
    [InlineData("duplicate")]
    public void Constructor_WhenEntryMeasurementsAreInvalid_RejectsExactCollection(string kind)
    {
        ImmutableArray<UsageMeasurement> measurements = kind switch
        {
            "default" => default,
            "null" => [null!],
            _ => [RunUsageTests.Measure(1), RunUsageTests.Measure(2)],
        };
        var exception = Should.Throw<ArgumentException>(() => RunUsageTests.Entry(1, measurements));
        exception.ParamName.ShouldBe("measurements");
        exception.GetType().ShouldBe(kind == "null" ? typeof(ArgumentNullException) : typeof(ArgumentException));
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = RunUsageTests.Measure(1);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
