// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationPlanProblemTests
{
    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationPlanProblem((EvaluationPlanProblemKind) 50, null, "m")).ParamName.ShouldBe("kind");

    [Fact]
    public void Constructor_WhenCaseIdIsPresentButBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new EvaluationPlanProblem(EvaluationPlanProblemKind.AgentNotFound, default(EvaluationCaseId), "m")).ParamName.ShouldBe("caseId");

    [Fact]
    public void Constructor_WhenMessageIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new EvaluationPlanProblem(EvaluationPlanProblemKind.AgentNotFound, null, " ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void Constructor_WhenPlanWide_LeavesTheCaseNull() =>
        new EvaluationPlanProblem(EvaluationPlanProblemKind.ExceedsLimits, null, "too many").CaseId.ShouldBeNull();
}
