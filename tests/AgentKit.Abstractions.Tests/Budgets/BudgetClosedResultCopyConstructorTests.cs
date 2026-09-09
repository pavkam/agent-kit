// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;

using AgentKit;

/// <summary>
/// Verifies that closed budget result records cannot be externally derived by
/// copying a built-in variant.
/// </summary>
public sealed class BudgetClosedResultCopyConstructorTests
{
    [Fact]
    public void BudgetBatchReservationResult_CopyConstructor_WhenExternalVariantCopiesBuiltIn_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new ForeignBudgetBatchReservationResult(BatchResult()));

        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void BudgetBatchReservationResult_CopyConstructor_WhenOriginalIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ForeignBudgetBatchReservationResult(null!));

        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void BudgetBatchReservationResult_CopyConstructor_WhenBuiltInVariantCopies_PreservesConcreteTypeAndValue()
    {
        var original = BatchResult();

        var copy = original with { };

        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
        copy.GetType().ShouldBe(typeof(BudgetBatchRejected));
        copy.Failure.ShouldBe(original.Failure);
    }

    [Fact]
    public void BudgetReservationResult_CopyConstructor_WhenExternalVariantCopiesBuiltIn_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new ForeignBudgetReservationResult(ReservationResult()));

        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void BudgetReservationResult_CopyConstructor_WhenOriginalIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ForeignBudgetReservationResult(null!));

        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void BudgetReservationResult_CopyConstructor_WhenBuiltInVariantCopies_PreservesConcreteTypeAndValue()
    {
        var original = ReservationResult();

        var copy = original with { };

        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
        copy.GetType().ShouldBe(typeof(BudgetRejected));
        copy.Failure.ShouldBe(original.Failure);
    }

    [Fact]
    public void BudgetScopeResult_CopyConstructor_WhenExternalVariantCopiesBuiltIn_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new ForeignBudgetScopeResult(ScopeResult()));

        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void BudgetScopeResult_CopyConstructor_WhenOriginalIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ForeignBudgetScopeResult(null!));

        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void BudgetScopeResult_CopyConstructor_WhenBuiltInVariantCopies_PreservesConcreteTypeAndValue()
    {
        var original = ScopeResult();

        var copy = original with { };

        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
        copy.GetType().ShouldBe(typeof(BudgetScopeCreationFailed));
        copy.Kind.ShouldBe(original.Kind);
        copy.SafeMessage.ShouldBe(original.SafeMessage);
    }

    [Fact]
    public void BudgetStartResult_CopyConstructor_WhenExternalVariantCopiesBuiltIn_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new ForeignBudgetStartResult(StartResult()));

        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void BudgetStartResult_CopyConstructor_WhenOriginalIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ForeignBudgetStartResult(null!));

        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void BudgetStartResult_CopyConstructor_WhenBuiltInVariantCopies_PreservesConcreteTypeAndValue()
    {
        var original = StartResult();

        var copy = original with { };

        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
        copy.GetType().ShouldBe(typeof(BudgetStarted));
        copy.ReservationId.ShouldBe(original.ReservationId);
        copy.WasAlreadyStarted.ShouldBe(original.WasAlreadyStarted);
    }

    private static BudgetBatchRejected BatchResult() => new(Failure());

    private static BudgetRejected ReservationResult() => new(Failure());

    private static BudgetScopeCreationFailed ScopeResult() => new(
        BudgetScopeCreationFailureKind.InvalidLimit,
        "The configured limit is invalid.");

    private static BudgetStarted StartResult() => new(
        new BudgetReservationId(Guid.Parse("d13b8897-1c31-4c64-b739-9193888bdf78")),
        wasAlreadyStarted: false);

    private static BudgetLimitFailure Failure() => new(
        new BudgetScopeId(Guid.Parse("b17ed825-764a-4eb3-b81c-b20b5e6c7679")),
        new BudgetDimension("agentkit.test"),
        BudgetLimitKind.Hard,
        configuredValue: 1m,
        observedValue: 1m,
        requestedAmount: 1m,
        new BudgetUnit("count"),
        "The hard limit is exhausted.");

    private sealed record ForeignBudgetBatchReservationResult(BudgetBatchReservationResult Original)
        : BudgetBatchReservationResult(Original);

    private sealed record ForeignBudgetReservationResult(BudgetReservationResult Original)
        : BudgetReservationResult(Original);

    private sealed record ForeignBudgetScopeResult(BudgetScopeResult Original)
        : BudgetScopeResult(Original);

    private sealed record ForeignBudgetStartResult(BudgetStartResult Original)
        : BudgetStartResult(Original);
}
