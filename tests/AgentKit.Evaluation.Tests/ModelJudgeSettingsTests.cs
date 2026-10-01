// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class ModelJudgeSettingsTests
{
    private static ModelJudgeSettings Build(
        ModelAlias? model = null,
        int repeat = 3,
        ModelJudgeBudget? budget = null,
        double deviation = 0.25,
        int characters = 100) =>
        new(model ?? new ModelAlias("judge"), repeat, budget ?? new ModelJudgeBudget(10), deviation, characters);

    [Fact]
    public void Constructor_WhenJudgeModelIsDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Build(model: default(ModelAlias))).ParamName.ShouldBe("judgeModel");

    [Fact]
    public void Constructor_WhenBudgetIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ModelJudgeSettings(new ModelAlias("j"), 1, null!, 0.1, 1)).ParamName.ShouldBe("budget");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenRepeatCountIsNotPositive_ThrowsArgumentOutOfRangeException(int repeat) =>
        Should.Throw<ArgumentOutOfRangeException>(() => Build(repeat: repeat)).ParamName.ShouldBe("repeatCount");

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void Constructor_WhenDeviationBoundIsInvalid_ThrowsArgumentOutOfRangeException(double deviation) =>
        Should.Throw<ArgumentOutOfRangeException>(() => Build(deviation: deviation)).ParamName.ShouldBe("maximumStandardDeviation");

    [Fact]
    public void Constructor_WhenCandidateBoundIsNotPositive_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Build(characters: 0)).ParamName.ShouldBe("maximumCandidateCharacters");

    [Fact]
    public void Equals_WhenEveryValueMatches_IsEqual() =>
        Build().ShouldBe(Build());
}
