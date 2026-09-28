// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

/// <summary>Evaluates unknown-cost reservations against captured runtime policy.</summary>
internal static class BudgetUnknownCostGuard
{
    /// <summary>Rejects an unknown cost reservation when policy and scope limits require it.</summary>
    internal static async ValueTask<BudgetLimitFailure?> EvaluateAsync(
        IBudgetLedger ledger,
        BudgetLedgerScopeReference scope,
        AgentBudgetOptionsSnapshot options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(options);

        var snapshot = await ledger.GetSnapshotAsync(scope, cancellationToken).ConfigureAwait(false);
        var hasLocalCostLimit = snapshot.Usages.Any(static usage =>
            usage.Dimension.Equals(BudgetDimensions.Cost) && usage.Limit is not null);

        return options.UnknownCostBehavior switch
        {
            BudgetUnknownCostBehavior.Reject when hasLocalCostLimit => CreateFailure(scope.Id, "Unknown cost cannot be reserved under a scope with a configured cost limit."),
            BudgetUnknownCostBehavior.AllowOnlyWithoutCostLimit when hasLocalCostLimit => CreateFailure(scope.Id, "Unknown cost cannot be reserved while a cost limit applies to this scope."),
            BudgetUnknownCostBehavior.Reject => null,
            BudgetUnknownCostBehavior.AllowOnlyWithoutCostLimit => null,
            _ => throw new UnreachableException(),
        };
    }

    private static BudgetLimitFailure CreateFailure(BudgetScopeId scopeId, string message) =>
        new(
            scopeId,
            BudgetDimensions.Cost,
            BudgetLimitKind.Hard,
            configuredValue: 0,
            observedValue: BudgetQuantity.FromDecimal(0),
            requestedAmount: BudgetQuantity.FromDecimal(1),
            new BudgetUnit("usd"),
            message);
}
