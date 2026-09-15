// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite;

/// <summary>Mutable composition-time input for the operational and evidence bounds of one SQLite budget-ledger singleton.</summary>
/// <remarks>
/// This type exists only so <c>AddSqliteBudgetLedger(target, configure)</c> can follow the standard .NET configure-delegate
/// pattern. Its defaults equal <see cref="SqliteBudgetLedgerSettings.CreateDefault"/>. The registration extension materializes
/// an immutable, validated <see cref="SqliteBudgetLedgerSettings"/> from the configured values at the composition boundary,
/// so invalid values throw <see cref="ArgumentOutOfRangeException"/> during registration rather than during a run. Instances are
/// never registered in the container and are not read after registration; the ledger only observes the materialized settings.
/// The type is not thread-safe and is intended to be mutated by a single configure delegate.
/// </remarks>
public sealed class SqliteBudgetLedgerOptions
{
    /// <summary>Gets or sets the finite provider lock-wait bound.</summary>
    /// <value>A positive whole-second duration representable by an <see cref="int"/> second count, used as SQLite's default and command timeout. Defaults to five seconds.</value>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(5);
    /// <summary>Gets or sets the maximum encoded immutable request-evidence size.</summary>
    /// <value>A positive byte count enforced before an encoded request is written and before a stored request is retained or decoded. Defaults to one mebibyte.</value>
    public int MaximumPayloadBytes { get; set; } = 1_048_576;
    /// <summary>Gets or sets the maximum encoded result-evidence size.</summary>
    /// <value>A positive byte count enforced before an encoded result is written and before a stored result is retained or decoded. Defaults to one mebibyte.</value>
    public int MaximumResultBytes { get; set; } = 1_048_576;
    /// <summary>Gets or sets the maximum ordered reservation count in one atomic batch.</summary>
    /// <value>A positive count enforced for atomic reservation batches. Defaults to 256.</value>
    public int MaximumBatchSize { get; set; } = 256;
    /// <summary>Gets or sets the maximum configured limit count per scope.</summary>
    /// <value>A positive count enforced before a scope is persisted. Defaults to 256.</value>
    public int MaximumLimitsPerScope { get; set; } = 256;
    /// <summary>Gets or sets the maximum scope ancestry depth.</summary>
    /// <value>A positive depth enforced for each persisted scope lineage. Defaults to 32.</value>
    public int MaximumLineageDepth { get; set; } = 32;
}
