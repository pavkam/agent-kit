// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class ModelJudgeBudgetStateTests
{
    [Fact]
    public void TryReserveCall_WhenCallsRemain_ReservesUntilTheBoundThenRefuses()
    {
        var state = new ModelJudgeBudgetState(new ModelJudgeBudget(2));

        state.TryReserveCall().ShouldBeTrue();
        state.TryReserveCall().ShouldBeTrue();
        state.TryReserveCall().ShouldBeFalse();
        state.Calls.ShouldBe(2);
    }

    [Fact]
    public void TryReserveCall_WhenTokensReachTheBound_RefusesEvenIfCallsRemain()
    {
        var state = new ModelJudgeBudgetState(new ModelJudgeBudget(10, 100));

        state.TryReserveCall().ShouldBeTrue();
        state.RecordTokens(60, 40);
        state.TryReserveCall().ShouldBeFalse();
        state.Tokens.ShouldBe(100);
        state.Calls.ShouldBe(1);
    }

    [Fact]
    public void RecordTokens_WhenUsageIsUnknown_AddsNothingInsteadOfGuessing()
    {
        var state = new ModelJudgeBudgetState(new ModelJudgeBudget(10, 100));

        state.RecordTokens(null, null);
        state.RecordTokens(7, null);

        state.Tokens.ShouldBe(7);
    }

    [Fact]
    public void TryReserveCall_WhenReservedConcurrently_NeverExceedsTheBound()
    {
        var state = new ModelJudgeBudgetState(new ModelJudgeBudget(25));
        var granted = 0;

        _ = Parallel.For(0, 400, _ =>
        {
            if (state.TryReserveCall())
            {
                _ = Interlocked.Increment(ref granted);
            }
        });

        granted.ShouldBe(25);
        state.Calls.ShouldBe(25);
    }
}
