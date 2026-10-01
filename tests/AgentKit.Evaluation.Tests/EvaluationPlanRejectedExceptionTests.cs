// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationPlanRejectedExceptionTests
{
    [Fact]
    public void Constructor_WhenProblemsAreDefaultEmptyOrContainNull_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new EvaluationPlanRejectedException(default)).ParamName.ShouldBe("problems");
        Should.Throw<ArgumentException>(() => new EvaluationPlanRejectedException([])).ParamName.ShouldBe("problems");
        Should.Throw<ArgumentException>(() => new EvaluationPlanRejectedException([null!])).ParamName.ShouldBe("problems");
    }

    [Fact]
    public void Constructor_WhenProblemsAreSupplied_ListsEverySafeMessageAndKeepsTheTypedProblems()
    {
        var first = new EvaluationPlanProblem(EvaluationPlanProblemKind.AgentNotFound, new EvaluationCaseId("a"), "agent missing");
        var second = new EvaluationPlanProblem(EvaluationPlanProblemKind.ExceedsLimits, null, "too many repetitions");

        var exception = new EvaluationPlanRejectedException([first, second]);

        exception.Problems.ShouldBe([first, second]);
        exception.Message.ShouldContain("agent missing");
        exception.Message.ShouldContain("too many repetitions");
    }
}
