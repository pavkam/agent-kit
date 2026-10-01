// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationScoreTests
{
    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_WhenValueIsOutsideZeroToOneOrNotFinite_ThrowsArgumentOutOfRangeException(double value) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationScore(value, 1, 0)).ParamName.ShouldBe("value");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenSampleCountIsNotPositive_ThrowsArgumentOutOfRangeException(int samples) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationScore(0.5, samples, 0)).ParamName.ShouldBe("sampleCount");

    [Theory]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_WhenDeviationIsNegativeOrNotFinite_ThrowsArgumentOutOfRangeException(double deviation) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationScore(0.5, 3, deviation)).ParamName.ShouldBe("standardDeviation");

    [Theory]
    [InlineData(0d)]
    [InlineData(1d)]
    [InlineData(0.625)]
    public void Constructor_WhenBoundaryValuesAreSupplied_PreservesThem(double value)
    {
        var score = new EvaluationScore(value, 4, 0.125);

        score.Value.ShouldBe(value);
        score.SampleCount.ShouldBe(4);
        score.StandardDeviation.ShouldBe(0.125);
    }

    [Fact]
    public void Certain_WhenPassedOrFailed_IsAnExactSingleSampleScore()
    {
        EvaluationScore.Certain(true).ShouldBe(new EvaluationScore(1, 1, 0));
        EvaluationScore.Certain(false).ShouldBe(new EvaluationScore(0, 1, 0));
    }
}
