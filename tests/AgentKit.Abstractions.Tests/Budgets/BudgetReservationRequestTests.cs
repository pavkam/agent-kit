// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;

/// <summary>Verifies BudgetReservationRequest behavior and contracts.</summary>
public sealed class BudgetReservationRequestTests
{
    [Fact]
    public void Constructor_WhenAmountIsZero_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetReservationRequest(
            new BudgetScopeId(Guid.NewGuid()),
            new BudgetDimension("tests.requests"),
            0m,
            new BudgetUnit("requests"),
            new OperationId(Guid.NewGuid()),
            null,
            new IdempotencyKey("tests.reservation")));

        exception.ParamName.ShouldBe("amount");
    }

    [Fact]
    public void Constructor_WhenAmountIsNegative_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetReservationRequest(
            new BudgetScopeId(Guid.NewGuid()),
            new BudgetDimension("tests.requests"),
            -1m,
            new BudgetUnit("requests"),
            new OperationId(Guid.NewGuid()),
            null,
            new IdempotencyKey("tests.reservation")));

        exception.ParamName.ShouldBe("amount");
    }

    [Fact]
    public void WithExpression_WhenAmountIsZero_RejectsCopy()
    {
        // Amount previously had only { get; init; }, so `request with { Amount = -5m }` or `{ Amount = 0m }`
        // produced a reservation request that bypassed the positive-amount invariant at its own boundary; the
        // ledger contract later rejected it under its own parameter name instead.
        var request = Request();
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => request with { Amount = 0m });
        exception.ParamName.ShouldBe("Amount");
    }

    [Fact]
    public void WithExpression_WhenAmountIsNegative_RejectsCopy()
    {
        var request = Request();
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => request with { Amount = -5m });
        exception.ParamName.ShouldBe("Amount");
    }

    [Fact]
    public void WithExpression_WhenAmountIsPositive_PreservesValue()
    {
        var request = Request();
        var copy = request with { Amount = 42m };
        copy.Amount.ShouldBe(42m);
    }

    private static BudgetReservationRequest Request() => new(
        new BudgetScopeId(Guid.NewGuid()),
        new BudgetDimension("tests.requests"),
        1m,
        new BudgetUnit("requests"),
        new OperationId(Guid.NewGuid()),
        null,
        new IdempotencyKey("tests.reservation"));
}
