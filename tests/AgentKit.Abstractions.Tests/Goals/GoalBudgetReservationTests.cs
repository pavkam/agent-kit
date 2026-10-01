// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

/// <summary>Verifies GoalBudgetReservation constraints.</summary>
public sealed class GoalBudgetReservationTests
{
    [Fact]
    public void Constructor_WhenBudgetIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new GoalBudgetReservation(null!)).ParamName.ShouldBe("budget");

    [Fact]
    public void Constructor_WhenScopeIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new GoalBudgetReservation(new GoalBudget(1, 1, 0), default(BudgetScopeId))).ParamName.ShouldBe("scopeId");

    [Fact]
    public void Constructor_WhenNoScopeIsGiven_MeansNotYetReserved() =>
        new GoalBudgetReservation(new GoalBudget(1, 1, 0)).ScopeId.ShouldBeNull();
}
