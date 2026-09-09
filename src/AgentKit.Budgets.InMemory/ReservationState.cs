// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.InMemory;

/// <summary>Retains one reservation's immutable receipt, accounting lifecycle, and replay records.</summary>
internal sealed class ReservationState
{
    /// <summary>Initializes an unstarted reservation after its full atomic batch is staged.</summary>
    /// <param name="receipt">The persisted receipt.</param>
    /// <param name="lineage">The initialized nonempty charged scope lineage.</param>
    /// <param name="aggregation">The captured dimension aggregation semantics.</param>
    /// <exception cref="ArgumentNullException"><paramref name="receipt"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="lineage"/> is default or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="aggregation"/> is undefined.</exception>
    internal ReservationState(BudgetLedgerReservationReceipt receipt, ImmutableArray<ScopeState> lineage, BudgetAggregationKind aggregation)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentException.ThrowIfDefaultOrEmpty(lineage);
        ArgumentOutOfRangeException.ThrowIfUndefined(aggregation);
        Receipt = receipt;
        Lineage = lineage;
        Aggregation = aggregation;
    }

    /// <summary>Gets immutable original and effective reservation evidence.</summary>
    internal BudgetLedgerReservationReceipt Receipt { get; }
    /// <summary>Gets the charged hierarchy from target scope through root.</summary>
    internal ImmutableArray<ScopeState> Lineage { get; }
    /// <summary>Gets the captured accounting aggregation.</summary>
    internal BudgetAggregationKind Aggregation { get; }
    /// <summary>Gets or sets the durable pre-effect start permission timestamp.</summary>
    internal DateTimeOffset? StartedAt { get; set; }
    /// <summary>Gets or sets the revision at which start permission was recorded.</summary>
    internal long StartRevision { get; set; }
    /// <summary>Gets or sets whether retained capacity was released.</summary>
    internal bool Released { get; set; }
    /// <summary>Gets or sets the current accounting after corrections.</summary>
    internal BudgetCommitResult? Commit { get; set; }
    /// <summary>Gets or sets the immutable first settlement used for exact settlement replay.</summary>
    internal BudgetCommitResult? OriginalCommit { get; set; }
    /// <summary>Gets or sets the latest positive ledger accounting revision.</summary>
    internal BudgetAccountingRevision? AccountingRevision { get; set; }
    /// <summary>Gets or sets the persisted rejection returned by every expired-start replay.</summary>
    /// <summary>Gets or sets the immutable expiration receipt persisted by direct or lazy expiry cleanup.</summary>
    internal BudgetStartExpired? StartExpiration { get; set; }
    /// <summary>Gets or sets the largest accepted correction revision.</summary>
    internal long LatestCorrectionRevision { get; set; }
    /// <summary>Gets correction receipts by caller-owned revision.</summary>
    internal Dictionary<long, BudgetCorrectionResult> Corrections { get; } = [];
    /// <summary>Gets reconciliation receipts by exact idempotency key.</summary>
    internal Dictionary<IdempotencyKey, ReconciliationState> Reconciliations { get; } = [];
    /// <summary>Gets whether this row still consumes reserved capacity.</summary>
    internal bool IsCapacityRetaining => !Released && Commit is null;
}
