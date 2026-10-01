// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationResultReceiptTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreInvalid_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationResultReceipt(default, new EvaluationCaseId("c"), 0, 1)).ParamName.ShouldBe("evaluationRunId");
        Should.Throw<ArgumentException>(() => new EvaluationResultReceipt(EvaluationTestData.RunId, default, 0, 1)).ParamName.ShouldBe("caseId");
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationResultReceipt(EvaluationTestData.RunId, new EvaluationCaseId("c"), -1, 1)).ParamName.ShouldBe("caseOrdinal");
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationResultReceipt(EvaluationTestData.RunId, new EvaluationCaseId("c"), 0, 0)).ParamName.ShouldBe("repetition");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_PreservesThem()
    {
        var receipt = new EvaluationResultReceipt(EvaluationTestData.RunId, new EvaluationCaseId("c"), 3, 2);

        (receipt.EvaluationRunId, receipt.CaseId, receipt.CaseOrdinal, receipt.Repetition)
            .ShouldBe((EvaluationTestData.RunId, new EvaluationCaseId("c"), 3, 2));
    }
}
