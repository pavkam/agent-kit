// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationOptionsSnapshotTests
{
    [Theory]
    [InlineData(0, 1, 1, "maximumConcurrentCases")]
    [InlineData(1, 0, 1, "maximumRepetitions")]
    [InlineData(1, 1, 0, "defaultCaseTimeout")]
    [InlineData(-1, 1, 1, "maximumConcurrentCases")]
    public void Constructor_WhenAValueIsNotPositive_ThrowsArgumentOutOfRangeException(int concurrency, int repetitions, int seconds, string parameter)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new EvaluationOptionsSnapshot(concurrency, repetitions, TimeSpan.FromSeconds(seconds)));

        exception.ParamName.ShouldBe(parameter);
    }

    [Fact]
    public void From_WhenOptionsAreNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => EvaluationOptionsSnapshot.From(null!)).ParamName.ShouldBe("options");

    [Fact]
    public void From_WhenOptionsChangeAfterwards_KeepsTheValuesCapturedAtCreation()
    {
        var options = new EvaluationOptions { MaximumConcurrentCases = 3, MaximumRepetitions = 2, DefaultCaseTimeout = TimeSpan.FromSeconds(9) };

        var snapshot = EvaluationOptionsSnapshot.From(options);
        options.MaximumConcurrentCases = 99;

        snapshot.MaximumConcurrentCases.ShouldBe(3);
        snapshot.MaximumRepetitions.ShouldBe(2);
        snapshot.DefaultCaseTimeout.ShouldBe(TimeSpan.FromSeconds(9));
    }
}
