// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <summary>Retains one replayed reservation's immutable receipt, accounting lifecycle, and replay bindings.</summary>
/// <remarks>
/// This is the in-memory projection of the journal, not a separate source of truth. A started reservation whose spend is
/// still unknown keeps <see cref="StartedAt"/> set and <see cref="Commit"/> null, which is exactly the state that survives
/// process loss and reappears from a recovery scan until reconciliation resolves it. Instances are mutated only while the
/// owning ledger's gate is held.
/// </remarks>
internal sealed class ReservationState
{
    /// <summary>Initializes an unstarted reservation after its full atomic batch is staged.</summary>
    /// <param name="receipt">The persisted receipt.</param>
    /// <param name="lineage">The initialized nonempty charged scope lineage from owning scope through root.</param>
    /// <param name="aggregation">The captured dimension aggregation semantics.</param>
    /// <exception cref="ArgumentNullException"><paramref name="receipt"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="lineage"/> is default or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="aggregation"/> is undefined.</exception>
    internal ReservationState(
        BudgetLedgerReservationReceipt receipt, ImmutableArray<ScopeState> lineage, BudgetAggregationKind aggregation)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentException.ThrowIfDefaultOrEmpty(lineage);
        ArgumentOutOfRangeException.ThrowIfUndefined(aggregation);
        Receipt = receipt;
        Lineage = lineage;
        Aggregation = aggregation;
    }

    /// <summary>Gets immutable original and effective reservation evidence.</summary>
    /// <value>The receipt returned by the accepting batch and by every exact replay of it.</value>
    internal BudgetLedgerReservationReceipt Receipt { get; }

    /// <summary>Gets the charged hierarchy from the owning scope through the root.</summary>
    /// <value>A nonempty ordered array; every boundary in it enforces this reservation's capacity.</value>
    internal ImmutableArray<ScopeState> Lineage { get; }

    /// <summary>Gets the captured accounting aggregation for this reservation's dimension.</summary>
    /// <value>The semantics used to combine reserved and committed quantities.</value>
    internal BudgetAggregationKind Aggregation { get; }

    /// <summary>Gets or sets the durable pre-effect start permission instant.</summary>
    /// <value>The recorded ledger-clock instant, or null while the reservation has not started.</value>
    internal DateTimeOffset? StartedAt { get; set; }

    /// <summary>Gets or sets the ledger revision at which start permission was recorded.</summary>
    /// <value>A monotonic revision used to anchor recovery-scan watermarks.</value>
    internal long StartRevision { get; set; }

    /// <summary>Gets or sets whether retained capacity was released.</summary>
    /// <value><see langword="true"/> after caller release, expiry sweep, or proven-no-usage reconciliation.</value>
    internal bool Released { get; set; }

    /// <summary>Gets or sets the current accounting after any corrections.</summary>
    /// <value>The latest commitment, or null while the reservation is unsettled.</value>
    internal BudgetCommitResult? Commit { get; set; }

    /// <summary>Gets or sets the immutable first settlement used for exact settlement replay.</summary>
    /// <value>The original commitment, which later corrections never replace.</value>
    internal BudgetCommitResult? OriginalCommit { get; set; }

    /// <summary>Gets or sets the latest positive ledger accounting revision for this reservation.</summary>
    /// <value>The revision that produced <see cref="Commit"/>, or null while unsettled.</value>
    internal BudgetAccountingRevision? AccountingRevision { get; set; }

    /// <summary>Gets or sets the immutable expiration receipt persisted by direct or lazy expiry cleanup.</summary>
    /// <value>The typed rejection returned by every later start attempt, or null when the reservation never expired.</value>
    internal BudgetStartExpired? StartExpiration { get; set; }

    /// <summary>Gets or sets the largest accepted caller correction revision.</summary>
    /// <value>A monotonic bound that rejects a reused or regressing revision.</value>
    internal long LatestCorrectionRevision { get; set; }

    /// <summary>Gets correction receipts keyed by caller-owned revision.</summary>
    /// <value>A mutable replay index populated as corrections commit.</value>
    internal Dictionary<long, BudgetCorrectionResult> Corrections { get; } = [];

    /// <summary>Gets reconciliation receipts keyed by exact idempotency key.</summary>
    /// <value>A mutable replay index that survives later settlement or release.</value>
    internal Dictionary<IdempotencyKey, ReconciliationState> Reconciliations { get; } = [];

    /// <summary>Gets whether this row still consumes reserved capacity.</summary>
    /// <value><see langword="true"/> while the reservation is neither released nor settled.</value>
    internal bool IsCapacityRetaining => !Released && Commit is null;
}
