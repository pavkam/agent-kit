// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.InMemory.Tests;

public sealed class EvaluationAppendPlanTests
{
    [Fact]
    public void ToResult_WhenEachKindIsPlanned_YieldsTheMatchingTypedAnswer()
    {
        var result = EvaluationResultConformanceData.Result(EvaluationResultConformanceData.NewRun(), 1, 2, "c");

        var applied = EvaluationAppendPlan.Apply(result).ToResult().ShouldBeOfType<EvaluationStoreAppended>();
        var replayed = EvaluationAppendPlan.Replay(result).ToResult().ShouldBeOfType<EvaluationStoreAppended>();
        var rejected = EvaluationAppendPlan.Reject(result, new EvaluationStoreFailure(EvaluationStoreFailureKind.LimitExceeded, "m")).ToResult();

        applied.Replayed.ShouldBeFalse();
        replayed.Replayed.ShouldBeTrue();
        applied.Receipt.ShouldBe(new EvaluationResultReceipt(result.EvaluationRunId, new EvaluationCaseId("c"), 1, 2));
        rejected.ShouldBeOfType<EvaluationStoreRejected>().Failure.Kind.ShouldBe(EvaluationStoreFailureKind.LimitExceeded);
    }
}
