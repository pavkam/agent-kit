// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;

using AgentKit;

/// <summary>Verifies BudgetReservationResult behavior and contracts.</summary>
public sealed class BudgetReservationResultTests
{
    [Fact]
    public void BudgetReservationResult_CopyConstructor_WhenExternalVariantCopiesBuiltIn_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ForeignBudgetReservationResult(ReservationResult()));
        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void BudgetReservationResult_CopyConstructor_WhenOriginalIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ForeignBudgetReservationResult(null!));
        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void BudgetReservationResult_CopyConstructor_WhenBuiltInVariantCopies_PreservesConcreteTypeAndValue()
    {
        var original = ReservationResult();
        var copy = original with
        {
        };
        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
        copy.GetType().ShouldBe(typeof(BudgetRejected));
        copy.Failure.ShouldBe(original.Failure);
    }

    private static BudgetRejected ReservationResult() => new(Failure());
    private static BudgetLimitFailure Failure() => new(new BudgetScopeId(Guid.Parse("b17ed825-764a-4eb3-b81c-b20b5e6c7679")), new BudgetDimension("agentkit.test"), BudgetLimitKind.Hard, configuredValue: 1m, observedValue: 1m, requestedAmount: 1m, new BudgetUnit("count"), "The hard limit is exhausted.");
    private sealed record ForeignBudgetReservationResult(BudgetReservationResult Original): BudgetReservationResult(Original);
}
