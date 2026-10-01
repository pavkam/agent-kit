// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationExecutionPolicyTests
{
    [Fact]
    public void Default_WhenRead_IsSerialSingleRepetitionWithNoLimits()
    {
        var policy = EvaluationExecutionPolicy.Default;

        policy.MaximumConcurrentCases.ShouldBe(1);
        policy.Repetitions.ShouldBe(1);
        policy.CaseTimeout.ShouldBeNull();
        policy.PlanDeadline.ShouldBeNull();
        policy.MaximumCaseRuns.ShouldBeNull();
        policy.StopOnEvaluatorFailure.ShouldBeFalse();
        policy.PermitUnsupportedEvaluators.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0, 1, "maximumConcurrentCases")]
    [InlineData(-1, 1, "maximumConcurrentCases")]
    [InlineData(1, 0, "repetitions")]
    [InlineData(1, -3, "repetitions")]
    public void Constructor_WhenCountIsNotPositive_ThrowsArgumentOutOfRangeException(int concurrency, int repetitions, string parameter)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new EvaluationExecutionPolicy(concurrency, repetitions, null, null, null, false, false));

        exception.ParamName.ShouldBe(parameter);
    }

    [Fact]
    public void Constructor_WhenCaseTimeoutIsNotPositive_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationExecutionPolicy(1, 1, TimeSpan.Zero, null, null, false, false))
            .ParamName.ShouldBe("caseTimeout");

    [Fact]
    public void Constructor_WhenPlanDeadlineIsNotPositive_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationExecutionPolicy(1, 1, null, TimeSpan.FromSeconds(-1), null, false, false))
            .ParamName.ShouldBe("planDeadline");

    [Fact]
    public void Constructor_WhenMaximumCaseRunsIsNotPositive_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationExecutionPolicy(1, 1, null, null, 0, false, false))
            .ParamName.ShouldBe("maximumCaseRuns");

    [Fact]
    public void Constructor_WhenEverythingIsValid_PreservesEveryDeclaration()
    {
        var policy = new EvaluationExecutionPolicy(4, 3, TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(1), 12, true, true);

        policy.MaximumConcurrentCases.ShouldBe(4);
        policy.Repetitions.ShouldBe(3);
        policy.CaseTimeout.ShouldBe(TimeSpan.FromSeconds(2));
        policy.PlanDeadline.ShouldBe(TimeSpan.FromMinutes(1));
        policy.MaximumCaseRuns.ShouldBe(12);
        policy.StopOnEvaluatorFailure.ShouldBeTrue();
        policy.PermitUnsupportedEvaluators.ShouldBeTrue();
    }
}
