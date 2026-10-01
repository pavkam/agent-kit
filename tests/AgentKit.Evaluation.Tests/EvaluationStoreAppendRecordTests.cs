// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationStoreAppendRecordTests
{
    private static EvaluationStoreAppended Appended() =>
        new EvaluationStoreAppended(new EvaluationResultReceipt(EvaluationTestData.RunId, new EvaluationCaseId("c"), 0, 1), false);

    [Fact]
    public void Constructor_WhenArgumentsAreInvalid_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentException>(() => new EvaluationStoreAppendRecord(default, 1, Appended())).ParamName.ShouldBe("caseId");
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationStoreAppendRecord(new EvaluationCaseId("c"), 0, Appended())).ParamName.ShouldBe("repetition");
        Should.Throw<ArgumentNullException>(() => new EvaluationStoreAppendRecord(new EvaluationCaseId("c"), 1, null!)).ParamName.ShouldBe("result");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_PreservesThem()
    {
        var record = new EvaluationStoreAppendRecord(new EvaluationCaseId("c"), 2, Appended());

        record.CaseId.ShouldBe(new EvaluationCaseId("c"));
        record.Repetition.ShouldBe(2);
    }
}
