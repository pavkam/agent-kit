// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;

using AgentKit;

/// <summary>Verifies BudgetStartResult behavior and contracts.</summary>
public sealed class BudgetStartResultTests
{
    [Fact]
    public void BudgetStartResult_CopyConstructor_WhenExternalVariantCopiesBuiltIn_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ForeignBudgetStartResult(StartResult()));
        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void BudgetStartResult_CopyConstructor_WhenOriginalIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ForeignBudgetStartResult(null!));
        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void BudgetStartResult_CopyConstructor_WhenBuiltInVariantCopies_PreservesConcreteTypeAndValue()
    {
        var original = StartResult();
        var copy = original with
        {
        };
        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
        copy.GetType().ShouldBe(typeof(BudgetStarted));
        copy.ReservationId.ShouldBe(original.ReservationId);
        copy.WasAlreadyStarted.ShouldBe(original.WasAlreadyStarted);
    }

    private static BudgetStarted StartResult() => new(new BudgetReservationId(Guid.Parse("d13b8897-1c31-4c64-b739-9193888bdf78")), wasAlreadyStarted: false);
    private sealed record ForeignBudgetStartResult(BudgetStartResult Original): BudgetStartResult(Original);
}
