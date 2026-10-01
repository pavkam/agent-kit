// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

public sealed class DefaultGoalBudgetManagerTests
{
    private static GoalBudgetReserveRequest Request(GoalBudget requested, GoalBudget parent)
    {
        var agent = GoalTestData.NewAgent();
        var session = GoalTestData.NewSession();
        var run = GoalTestData.NewRun();
        var goal = GoalTestData.Goal(agent, session, run, budget: parent);
        var delegation = GoalTestData.Delegation(goal, new GoalAttemptId(Guid.NewGuid()), run, GoalTestData.NewAgent(), "k", GoalTestData.Authorization(agent, session, run), requested);
        return new GoalBudgetReserveRequest(delegation, parent, null);
    }

    [Fact]
    public async Task ReserveAsync_WhenRequestIsNull_ThrowsArgumentNullException() =>
        (await Should.ThrowAsync<ArgumentNullException>(async () => await new DefaultGoalBudgetManager().ReserveAsync(null!, TestContext.Current.CancellationToken))).ParamName.ShouldBe("request");

    [Fact]
    public async Task ReserveAsync_WhenChildFitsInsideParentWithoutAuthority_ReservesWithoutScope()
    {
        var result = await new DefaultGoalBudgetManager().ReserveAsync(Request(new GoalBudget(2, 3, 0), new GoalBudget(10, 10, 4)), TestContext.Current.CancellationToken);

        var reserved = result.ShouldBeOfType<GoalBudgetReserved>();
        reserved.Reservation.Budget.ShouldBe(new GoalBudget(2, 3, 0));
        reserved.Reservation.ScopeId.ShouldBeNull();
    }

    [Theory]
    [InlineData(11, 1, 0)]
    [InlineData(1, 11, 0)]
    [InlineData(1, 1, 4)]
    public async Task ReserveAsync_WhenChildIsNotStrictlyNarrower_RejectsAsBudgetUnavailable(int turns, int calls, int children)
    {
        var result = await new DefaultGoalBudgetManager().ReserveAsync(Request(new GoalBudget(turns, calls, children), new GoalBudget(10, 10, 4)), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<GoalBudgetRejected>().Rejection.Kind.ShouldBe(DelegationRejectionKind.BudgetUnavailable);
    }

    [Fact]
    public async Task SettleAsync_WhenKnownUsageStaysWithinCeiling_IsWithinBudget()
    {
        var reservation = new GoalBudgetReservation(new GoalBudget(3, 3, 1));

        var settlement = await new DefaultGoalBudgetManager().SettleAsync(new GoalBudgetSettleRequest(reservation, new GoalBudgetUsage(3, 2, 0, totalTokens: null)), TestContext.Current.CancellationToken);

        settlement.WithinBudget.ShouldBeTrue();
    }

    [Fact]
    public async Task SettleAsync_WhenUsageExceedsCeiling_IsNotWithinBudget()
    {
        var reservation = new GoalBudgetReservation(new GoalBudget(3, 3, 1));

        var settlement = await new DefaultGoalBudgetManager().SettleAsync(new GoalBudgetSettleRequest(reservation, new GoalBudgetUsage(4, 0, 0)), TestContext.Current.CancellationToken);

        settlement.WithinBudget.ShouldBeFalse();
    }
}
