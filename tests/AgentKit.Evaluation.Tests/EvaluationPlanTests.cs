// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationPlanTests
{
    [Fact]
    public void Constructor_WhenIdIsDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new EvaluationPlan(default, new EvaluationPlanVersion(1), [EvaluationTestData.Case()], EvaluationExecutionPolicy.Default, EvaluationRecordingPolicy.None))
            .ParamName.ShouldBe("id");

    [Fact]
    public void Constructor_WhenVersionIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationPlan(new EvaluationPlanId("p"), default, [EvaluationTestData.Case()], EvaluationExecutionPolicy.Default, EvaluationRecordingPolicy.None))
            .ParamName.ShouldBe("version");

    [Fact]
    public void Constructor_WhenCasesAreDefaultOrEmpty_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new EvaluationPlan(new EvaluationPlanId("p"), new EvaluationPlanVersion(1), default, EvaluationExecutionPolicy.Default, EvaluationRecordingPolicy.None))
            .ParamName.ShouldBe("cases");
        Should.Throw<ArgumentException>(() => new EvaluationPlan(new EvaluationPlanId("p"), new EvaluationPlanVersion(1), [], EvaluationExecutionPolicy.Default, EvaluationRecordingPolicy.None))
            .ParamName.ShouldBe("cases");
    }

    [Fact]
    public void Constructor_WhenACaseIsNull_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new EvaluationPlan(new EvaluationPlanId("p"), new EvaluationPlanVersion(1), [null!], EvaluationExecutionPolicy.Default, EvaluationRecordingPolicy.None))
            .ParamName.ShouldBe("cases");

    [Fact]
    public void Constructor_WhenCaseIdentitiesRepeat_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => EvaluationTestData.Plan([EvaluationTestData.Case("dup"), EvaluationTestData.Case("dup")]))
            .ParamName.ShouldBe("cases");

    [Fact]
    public void Constructor_WhenPoliciesAreNull_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new EvaluationPlan(new EvaluationPlanId("p"), new EvaluationPlanVersion(1), [EvaluationTestData.Case()], null!, EvaluationRecordingPolicy.None))
            .ParamName.ShouldBe("execution");
        Should.Throw<ArgumentNullException>(() => new EvaluationPlan(new EvaluationPlanId("p"), new EvaluationPlanVersion(1), [EvaluationTestData.Case()], EvaluationExecutionPolicy.Default, null!))
            .ParamName.ShouldBe("recording");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_PreservesCaseOrder()
    {
        var plan = EvaluationTestData.Plan([EvaluationTestData.Case("b"), EvaluationTestData.Case("a")]);

        plan.Cases.Select(static c => c.Id.Value).ShouldBe(["b", "a"]);
    }

    [Fact]
    public void Equals_WhenEveryMemberMatchesByValue_IsEqualAndHashesAlike()
    {
        var input = EvaluationTestData.Input();
        var left = EvaluationTestData.Plan([EvaluationTestData.Case("a", input: input)]);
        var right = EvaluationTestData.Plan([EvaluationTestData.Case("a", input: input)]);

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
        left.ShouldNotBe(EvaluationTestData.Plan([EvaluationTestData.Case("a", input: input)], version: 2));
    }
}
