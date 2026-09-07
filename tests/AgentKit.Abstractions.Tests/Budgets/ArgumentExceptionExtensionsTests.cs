// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;

public sealed class ArgumentExceptionExtensionsTests
{
    [Fact]
    public void ThrowIfInvalidBudgetReservationBatch_WhenValid_DoesNotThrow()
    {
        var scopeId = new BudgetScopeId(Guid.NewGuid());
        var operationId = new OperationId(Guid.NewGuid());
        var requests = CreateRequests(scopeId, operationId);

        Should.NotThrow(() => ArgumentException.ThrowIfInvalidBudgetReservationBatch(requests, scopeId));
    }

    [Fact]
    public void ThrowIfInvalidBudgetReservationBatch_WhenDefault_ThrowsWithInferredParameterName()
    {
        ImmutableArray<BudgetReservationRequest> requests = default;
        var scopeId = new BudgetScopeId(Guid.NewGuid());

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfInvalidBudgetReservationBatch(requests, scopeId));

        exception.ParamName.ShouldBe("requests");
    }

    [Fact]
    public void ThrowIfInvalidBudgetReservationBatch_WhenMemberIsNull_ThrowsWithInferredParameterName()
    {
        ImmutableArray<BudgetReservationRequest> requests = [null!];
        var scopeId = new BudgetScopeId(Guid.NewGuid());

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfInvalidBudgetReservationBatch(requests, scopeId));

        exception.ParamName.ShouldBe("requests");
    }

    [Fact]
    public void ThrowIfInvalidBudgetReservationBatch_WhenScopeDiffers_ThrowsBeforeAdmission()
    {
        var scopeId = new BudgetScopeId(Guid.NewGuid());
        var requests = CreateRequests(new BudgetScopeId(Guid.NewGuid()), new OperationId(Guid.NewGuid()));

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfInvalidBudgetReservationBatch(requests, scopeId));

        exception.ParamName.ShouldBe("requests");
    }

    [Fact]
    public void ThrowIfInvalidBudgetReservationBatch_WhenOperationDiffers_ThrowsBeforeAdmission()
    {
        var scopeId = new BudgetScopeId(Guid.NewGuid());
        var requests = CreateRequests(scopeId, new OperationId(Guid.NewGuid()))
            .SetItem(1, CreateRequest(scopeId, new OperationId(Guid.NewGuid()), "two"));

        _ = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfInvalidBudgetReservationBatch(requests, scopeId));
    }

    [Fact]
    public void ThrowIfInvalidBudgetReservationBatch_WhenItemKeyRepeats_ThrowsBeforeAdmission()
    {
        var scopeId = new BudgetScopeId(Guid.NewGuid());
        var operationId = new OperationId(Guid.NewGuid());
        var requests = CreateRequests(scopeId, operationId)
            .SetItem(1, CreateRequest(scopeId, operationId, "one"));

        _ = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfInvalidBudgetReservationBatch(requests, scopeId));
    }

    [Fact]
    public void ThrowIfInvalidBudgetReservationBatch_WhenDimensionUsesDifferentUnits_ThrowsBeforeAdmission()
    {
        var scopeId = new BudgetScopeId(Guid.NewGuid());
        var operationId = new OperationId(Guid.NewGuid());
        var requests = CreateRequests(scopeId, operationId).SetItem(
            1,
            new BudgetReservationRequest(
                scopeId,
                new BudgetDimension("tests.calls"),
                1m,
                new BudgetUnit("other"),
                operationId,
                null,
                new IdempotencyKey("two")));

        _ = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfInvalidBudgetReservationBatch(requests, scopeId));
    }

    private static ImmutableArray<BudgetReservationRequest> CreateRequests(
        BudgetScopeId scopeId,
        OperationId operationId) =>
        [CreateRequest(scopeId, operationId, "one"), CreateRequest(scopeId, operationId, "two")];

    private static BudgetReservationRequest CreateRequest(
        BudgetScopeId scopeId,
        OperationId operationId,
        string itemKey) =>
        new(
            scopeId,
            new BudgetDimension("tests.calls"),
            1m,
            new BudgetUnit("calls"),
            operationId,
            null,
            new IdempotencyKey(itemKey));
}
