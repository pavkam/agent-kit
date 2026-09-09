// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite;

/// <summary>Represents one integrity-checked reservation row loaded inside an immediate transaction.</summary>
internal sealed record SqliteReservationRow
{
    /// <summary>Creates a validated row projection.</summary>
    /// <param name="receipt">The non-null immutable reservation receipt.</param><param name="aggregation">The captured defined aggregation.</param>
    /// <param name="startedAt">The proven start instant, or null.</param><param name="startRevision">The nonnegative start revision.</param>
    /// <param name="released">Whether capacity was released.</param><param name="startExpiration">The persisted expiry rejection, or null.</param>
    /// <param name="originalCommit">The first settlement receipt, or null.</param><param name="currentCommit">The latest corrected settlement receipt, or null.</param>
    /// <param name="accountingRevision">The latest accounting revision, or null.</param><param name="latestCorrectionRevision">The nonnegative correction revision.</param>
    /// <exception cref="ArgumentNullException"><paramref name="receipt"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="aggregation"/> is undefined or a revision is negative.</exception>
    internal SqliteReservationRow(BudgetLedgerReservationReceipt receipt, BudgetAggregationKind aggregation, DateTimeOffset? startedAt, long startRevision, bool released, BudgetStartExpired? startExpiration, BudgetCommitResult? originalCommit, BudgetCommitResult? currentCommit, BudgetAccountingRevision? accountingRevision, long latestCorrectionRevision)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentOutOfRangeException.ThrowIfUndefined(aggregation);
        ArgumentOutOfRangeException.ThrowIfNegative(startRevision);
        ArgumentOutOfRangeException.ThrowIfNegative(latestCorrectionRevision);
        Receipt = receipt;
        Aggregation = aggregation;
        StartedAt = startedAt;
        StartRevision = startRevision;
        Released = released;
        StartExpiration = startExpiration;
        OriginalCommit = originalCommit;
        CurrentCommit = currentCommit;
        AccountingRevision = accountingRevision;
        LatestCorrectionRevision = latestCorrectionRevision;
    }

    /// <summary>Gets immutable reservation evidence.</summary><value>The non-null exact receipt.</value>
    internal BudgetLedgerReservationReceipt Receipt { get; }
    /// <summary>Gets captured aggregation semantics.</summary><value>A defined aggregation kind.</value>
    internal BudgetAggregationKind Aggregation { get; }
    /// <summary>Gets proven start time.</summary><value>The persisted instant, or null before start.</value>
    internal DateTimeOffset? StartedAt { get; }
    /// <summary>Gets start ordering evidence.</summary><value>A nonnegative ledger revision.</value>
    internal long StartRevision { get; }
    /// <summary>Gets whether retained capacity was released.</summary><value>True after expiry, explicit release, or no-usage reconciliation.</value>
    internal bool Released { get; }
    /// <summary>Gets replayable expiry evidence.</summary><value>The exact rejection, or null when no expiry rejection occurred.</value>
    internal BudgetStartExpired? StartExpiration { get; }
    /// <summary>Gets the immutable first settlement.</summary><value>The original receipt, or null while unresolved.</value>
    internal BudgetCommitResult? OriginalCommit { get; }
    /// <summary>Gets current settlement accounting.</summary><value>The original or corrected receipt, or null while unresolved.</value>
    internal BudgetCommitResult? CurrentCommit { get; }
    /// <summary>Gets latest accounting generation.</summary><value>The persisted positive revision, or null before accounting.</value>
    internal BudgetAccountingRevision? AccountingRevision { get; }
    /// <summary>Gets correction replay ordering.</summary><value>A nonnegative revision.</value>
    internal long LatestCorrectionRevision { get; }
    /// <summary>Gets whether this row still retains admission capacity.</summary><value>True only before release or settlement.</value>
    internal bool IsCapacityRetaining => !Released && CurrentCommit is null;
}
