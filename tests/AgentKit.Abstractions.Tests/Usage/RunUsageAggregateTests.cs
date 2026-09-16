// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Usage;
/// <summary>Verifies RunUsageAggregate behavior and contracts.</summary>
public sealed class RunUsageAggregateTests
{
    [Theory]
    [InlineData("dimension", "dimension")]
    [InlineData("unit", "unit")]
    [InlineData("aggregation", "aggregation")]
    [InlineData("default", "measurements")]
    [InlineData("null", "measurements")]
    [InlineData("foreign-dimension", "measurements")]
    [InlineData("foreign-unit", "measurements")]
    public void Constructor_WhenAggregateArgumentsAreInvalid_RejectsBeforeCalculating(string invalid, string parameter)
    {
        ImmutableArray<UsageMeasurement> measurements = invalid switch
        {
            "default" => default,
            "null" => [null!],
            "foreign-dimension" => [new(BudgetDimensions.OutputTokens, RunUsageTests.Tokens, null, UsageMeasurementQuality.Unknown)],
            "foreign-unit" => [new(BudgetDimensions.InputTokens, new BudgetUnit("other"), null, UsageMeasurementQuality.Unknown)],
            _ => [],
        };
        var exception = Should.Throw<ArgumentException>(() => new RunUsageAggregate(invalid == "dimension" ? default : BudgetDimensions.InputTokens, invalid == "unit" ? default : RunUsageTests.Tokens, invalid == "aggregation" ? (BudgetAggregationKind) (-1) : BudgetAggregationKind.Sum, measurements));
        exception.ParamName.ShouldBe(parameter);
        exception.GetType().ShouldBe(invalid == "null" ? typeof(ArgumentNullException) : invalid is "dimension" or "unit" or "aggregation" ? typeof(ArgumentOutOfRangeException) : typeof(ArgumentException));
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new RunUsageAggregate(BudgetDimensions.InputTokens, RunUsageTests.Tokens, BudgetAggregationKind.Sum, [RunUsageTests.Measure(1)]);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
