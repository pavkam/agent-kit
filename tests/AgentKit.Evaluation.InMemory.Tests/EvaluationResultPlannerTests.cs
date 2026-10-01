// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.InMemory.Tests;

public sealed class EvaluationResultPlannerTests
{
    [Fact]
    public void PlanAppend_WhenResultIsNew_AppliesIt()
    {
        var plan = EvaluationResultPlanner.PlanAppend(new EvaluationResultState(), EvaluationResultConformanceData.Result(EvaluationResultConformanceData.NewRun()));

        plan.Kind.ShouldBe(EvaluationAppendPlanKind.Applied);
        plan.ToResult().ShouldBeOfType<EvaluationStoreAppended>().Replayed.ShouldBeFalse();
    }

    [Fact]
    public void PlanAppend_WhenIdenticalResultExists_ReplaysTheRecordedOne()
    {
        var state = new EvaluationResultState();
        var run = EvaluationResultConformanceData.NewRun();
        state.Add(EvaluationResultConformanceData.Result(run));

        var plan = EvaluationResultPlanner.PlanAppend(state, EvaluationResultConformanceData.Result(run));

        plan.Kind.ShouldBe(EvaluationAppendPlanKind.Replayed);
        plan.ToResult().ShouldBeOfType<EvaluationStoreAppended>().Replayed.ShouldBeTrue();
    }

    [Theory]
    [InlineData("plan", 1, 0, 1, "case-0", 99.0)]
    [InlineData("other", 1, 5, 1, "case-5", 25.5)]
    [InlineData("plan", 2, 5, 1, "case-5", 25.5)]
    [InlineData("plan", 1, 0, 2, "intruder", 25.5)]
    [InlineData("plan", 1, 7, 1, "case-0", 25.5)]
    public void PlanAppend_WhenResultContradictsRecordedEvidence_RejectsWithIdentityConflict(string plan, long version, int ordinal, int repetition, string caseId, double latency)
    {
        var state = new EvaluationResultState();
        var run = EvaluationResultConformanceData.NewRun();
        state.Add(EvaluationResultConformanceData.Result(run));

        var decision = EvaluationResultPlanner.PlanAppend(state, EvaluationResultConformanceData.Result(run, ordinal, repetition, caseId, plan, version, latency));

        decision.Kind.ShouldBe(EvaluationAppendPlanKind.Rejected);
        decision.Failure!.Kind.ShouldBe(EvaluationStoreFailureKind.IdentityConflict);
        _ = decision.ToResult().ShouldBeOfType<EvaluationStoreRejected>();
    }

    [Fact]
    public void Read_WhenMoreResultsRemain_ReturnsAContinuationAtTheLastReturnedPosition()
    {
        var state = new EvaluationResultState();
        var run = EvaluationResultConformanceData.NewRun();
        foreach (var ordinal in Enumerable.Range(0, 4))
        {
            state.Add(EvaluationResultConformanceData.Result(run, ordinal));
        }

        var page = EvaluationResultPlanner.Read(state, new EvaluationResultQuery(run, 3));
        var last = EvaluationResultPlanner.Read(state, new EvaluationResultQuery(run, 3, page.Next));

        page.Results.Length.ShouldBe(3);
        page.Next.ShouldBe(new EvaluationResultCursor(2, 1));
        last.Results.Select(static r => r.CaseOrdinal).ShouldBe([3]);
        last.Next.ShouldBeNull();
    }
}
