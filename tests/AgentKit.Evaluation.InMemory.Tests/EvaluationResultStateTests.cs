// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.InMemory.Tests;

public sealed class EvaluationResultStateTests
{
    [Fact]
    public void Lookups_WhenNothingIsRecorded_ReturnNullAndEmpty()
    {
        var state = new EvaluationResultState();
        var run = EvaluationResultConformanceData.NewRun();

        state.FindRun(run).ShouldBeNull();
        state.Find(run, 0, 1).ShouldBeNull();
        state.FindCaseAt(run, 0).ShouldBeNull();
        state.FindOrdinalOf(run, new EvaluationCaseId("c")).ShouldBeNull();
        state.Read(run, null, 5).ShouldBeEmpty();
    }

    [Fact]
    public void Add_WhenResultsAreRecorded_PinsTheRunAndIndexesByPositionAndCase()
    {
        var state = new EvaluationResultState();
        var run = EvaluationResultConformanceData.NewRun();
        var result = EvaluationResultConformanceData.Result(run, 2, 3, "named", "plan-x", 4);

        state.Add(result);

        state.FindRun(run).ShouldBe(new EvaluationRunPin(new EvaluationPlanId("plan-x"), new EvaluationPlanVersion(4)));
        state.Find(run, 2, 3).ShouldBe(result);
        state.FindCaseAt(run, 2).ShouldBe(new EvaluationCaseId("named"));
        state.FindOrdinalOf(run, new EvaluationCaseId("named")).ShouldBe(2);
    }

    [Fact]
    public void Read_WhenAppendedOutOfOrder_OrdersByOrdinalThenRepetitionAndHonorsCursorAndLimit()
    {
        var state = new EvaluationResultState();
        var run = EvaluationResultConformanceData.NewRun();
        foreach (var (ordinal, repetition) in new[] { (1, 1), (0, 2), (0, 1), (1, 2) })
        {
            state.Add(EvaluationResultConformanceData.Result(run, ordinal, repetition));
        }

        state.Read(run, null, 10).Select(static r => (r.CaseOrdinal, r.Repetition)).ShouldBe([(0, 1), (0, 2), (1, 1), (1, 2)]);
        state.Read(run, new EvaluationResultCursor(0, 2), 10).Select(static r => (r.CaseOrdinal, r.Repetition)).ShouldBe([(1, 1), (1, 2)]);
        state.Read(run, null, 2).Count.ShouldBe(2);
    }
}
