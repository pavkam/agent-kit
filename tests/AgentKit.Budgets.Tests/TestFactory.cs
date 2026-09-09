// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Tests;

internal static class TestFactory
{
    public static readonly BudgetUnit Count = new("count");
    public static readonly BudgetDimension TestDimension = new("test.dimension");

    public static BudgetScopeAddress Address() =>
        new(new TenantId("tenant-1"), new PrincipalId("user-1"), new AgentId(Guid.NewGuid()), null, null, null);

    public static BudgetScopeRequest ScopeRequest(BudgetScopeId? parentScopeId = null) =>
        new(parentScopeId, Address(), [], new IdempotencyKey(Guid.NewGuid().ToString()));

    public static BudgetReservationRequest ReservationRequest(
        BudgetScopeId scopeId, decimal amount = 1m, OperationId? operationId = null) =>
        new(scopeId, TestDimension, amount, Count, operationId ?? new OperationId(Guid.NewGuid()), null, new IdempotencyKey(Guid.NewGuid().ToString()));

    public static AgentBudgetOptionsSnapshot DefaultOptions(
        BudgetOverrunBehavior overrunBehavior = BudgetOverrunBehavior.RecordAndBlockFurtherReservations) =>
        new(16, 256, TimeSpan.FromMinutes(5), BudgetUnknownCostBehavior.AllowOnlyWithoutCostLimit, overrunBehavior);

    public static BudgetLedgerReservationReceipt Receipt(BudgetLedgerScopeReference scope, BudgetReservationRequest request) =>
        new(new BudgetLedgerReservationReference(scope, new BudgetReservationId(Guid.NewGuid())), request, new BudgetEffectiveReservation(DateTimeOffset.UtcNow.AddMinutes(5)));
}
