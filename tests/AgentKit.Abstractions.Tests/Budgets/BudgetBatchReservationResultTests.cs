// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;

using AgentKit;

/// <summary>Verifies BudgetBatchReservationResult behavior and contracts.</summary>
public sealed class BudgetBatchReservationResultTests
{
    [Fact]
    public void BudgetBatchReservationResult_CopyConstructor_WhenExternalVariantCopiesBuiltIn_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ForeignBudgetBatchReservationResult(BatchResult()));
        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void BudgetBatchReservationResult_CopyConstructor_WhenOriginalIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ForeignBudgetBatchReservationResult(null!));
        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void BudgetBatchReservationResult_CopyConstructor_WhenBuiltInVariantCopies_PreservesConcreteTypeAndValue()
    {
        var original = BatchResult();
        var copy = original with
        {
        };
        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
        copy.GetType().ShouldBe(typeof(BudgetBatchRejected));
        copy.Failure.ShouldBe(original.Failure);
    }

    private static BudgetBatchRejected BatchResult() => new(Failure());
    private static BudgetLimitFailure Failure() => new(new BudgetScopeId(Guid.Parse("b17ed825-764a-4eb3-b81c-b20b5e6c7679")), new BudgetDimension("agentkit.test"), BudgetLimitKind.Hard, configuredValue: 1m, observedValue: 1m, requestedAmount: 1m, new BudgetUnit("count"), "The hard limit is exhausted.");
    private sealed record ForeignBudgetBatchReservationResult(BudgetBatchReservationResult Original): BudgetBatchReservationResult(Original);
}
