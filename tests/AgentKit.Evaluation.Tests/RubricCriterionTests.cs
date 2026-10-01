// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class RubricCriterionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Constructor_WhenRubricIsBlank_ThrowsArgumentException(string? rubric) =>
        Should.Throw<ArgumentException>(() => new RubricCriterion(rubric!)).ParamName.ShouldBe("rubric");

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Constructor_WhenScaleIsNotPositive_ThrowsArgumentOutOfRangeException(int scale) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RubricCriterion("r", scale)).ParamName.ShouldBe("scaleMaximum");

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_WhenThresholdIsOutsideZeroToOneOrNotFinite_ThrowsArgumentOutOfRangeException(double threshold) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RubricCriterion("r", 5, threshold)).ParamName.ShouldBe("passThreshold");

    [Fact]
    public void Constructor_WhenDefaults_UsesAFivePointScaleAndASeventyPercentThreshold()
    {
        var criterion = new RubricCriterion("Answers politely.");

        criterion.Key.ShouldBe(RubricCriterion.CriterionKey);
        criterion.Key.Value.ShouldBe("rubric");
        (criterion.Rubric, criterion.ScaleMaximum, criterion.PassThreshold).ShouldBe(("Answers politely.", 5, 0.7));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(1d)]
    public void Constructor_WhenThresholdIsAtABoundary_AcceptsIt(double threshold) =>
        new RubricCriterion("r", 1, threshold).PassThreshold.ShouldBe(threshold);
}
