// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Tests;

/// <summary>Verifies AgentBudgetOptionsSnapshot behavior and contracts.</summary>
public sealed class AgentBudgetOptionsSnapshotTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesExactCapturedValues()
    {
        var lifetime = TimeSpan.FromMinutes(3);
        var snapshot = new AgentBudgetOptionsSnapshot(
            4, 128, lifetime, BudgetUnknownCostBehavior.AllowOnlyWithoutCostLimit, BudgetOverrunBehavior.RequireOperatorReconciliation);
        snapshot.MaximumScopeDepth.ShouldBe(4);
        snapshot.MaximumOpenReservationsPerScope.ShouldBe(128);
        snapshot.DefaultReservationLifetime.ShouldBe(lifetime);
        snapshot.UnknownCostBehavior.ShouldBe(BudgetUnknownCostBehavior.AllowOnlyWithoutCostLimit);
        snapshot.OverrunBehavior.ShouldBe(BudgetOverrunBehavior.RequireOperatorReconciliation);
    }

    [Fact]
    public void RecordEquality_WhenFieldsMatch_AreEqualAndShareHashCode()
    {
        var lifetime = TimeSpan.FromMinutes(3);
        var first = new AgentBudgetOptionsSnapshot(
            4, 128, lifetime, BudgetUnknownCostBehavior.AllowOnlyWithoutCostLimit, BudgetOverrunBehavior.RequireOperatorReconciliation);
        var second = new AgentBudgetOptionsSnapshot(
            4, 128, lifetime, BudgetUnknownCostBehavior.AllowOnlyWithoutCostLimit, BudgetOverrunBehavior.RequireOperatorReconciliation);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ToString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void WithExpression_WhenNoFieldChanges_ClonesEveryField()
    {
        var lifetime = TimeSpan.FromMinutes(3);
        var original = new AgentBudgetOptionsSnapshot(
            4, 128, lifetime, BudgetUnknownCostBehavior.AllowOnlyWithoutCostLimit, BudgetOverrunBehavior.RequireOperatorReconciliation);
        var copy = original with { };
        copy.ShouldNotBeSameAs(original);
        copy.ShouldBe(original);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    public void Constructor_WhenMaximumScopeDepthIsNotPositive_ThrowsArgumentOutOfRangeException(int depth, int openReservations)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new AgentBudgetOptionsSnapshot(
            depth, openReservations, TimeSpan.FromMinutes(1), BudgetUnknownCostBehavior.AllowOnlyWithoutCostLimit, BudgetOverrunBehavior.RecordAndBlockFurtherReservations)).ParamName.ShouldBe("maximumScopeDepth");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaximumOpenReservationsIsNotPositive_ThrowsArgumentOutOfRangeException(int openReservations)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new AgentBudgetOptionsSnapshot(
            1, openReservations, TimeSpan.FromMinutes(1), BudgetUnknownCostBehavior.AllowOnlyWithoutCostLimit, BudgetOverrunBehavior.RecordAndBlockFurtherReservations)).ParamName.ShouldBe("maximumOpenReservationsPerScope");
    }

    [Fact]
    public void Constructor_WhenDefaultReservationLifetimeIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new AgentBudgetOptionsSnapshot(
            1, 1, TimeSpan.Zero, BudgetUnknownCostBehavior.AllowOnlyWithoutCostLimit, BudgetOverrunBehavior.RecordAndBlockFurtherReservations)).ParamName.ShouldBe("defaultReservationLifetime");
    }

    [Fact]
    public void Constructor_WhenUnknownCostBehaviorIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new AgentBudgetOptionsSnapshot(
            1, 1, TimeSpan.FromMinutes(1), (BudgetUnknownCostBehavior) (-1), BudgetOverrunBehavior.RecordAndBlockFurtherReservations)).ParamName.ShouldBe("unknownCostBehavior");
    }

    [Fact]
    public void Constructor_WhenOverrunBehaviorIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new AgentBudgetOptionsSnapshot(
            1, 1, TimeSpan.FromMinutes(1), BudgetUnknownCostBehavior.AllowOnlyWithoutCostLimit, (BudgetOverrunBehavior) (-1))).ParamName.ShouldBe("overrunBehavior");
    }
}
