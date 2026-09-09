// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite;

/// <summary>Captures immutable positive operational and evidence bounds for one SQLite budget-ledger singleton.</summary>
public sealed record SqliteBudgetLedgerSettings
{
    /// <summary>Initializes explicit lock-wait and evidence bounds.</summary>
    /// <param name="lockTimeout">The positive whole-second SQLite busy/locked wait bound representable by the provider.</param>
    /// <param name="maximumPayloadBytes">The positive maximum encoded immutable request-evidence size.</param>
    /// <param name="maximumResultBytes">The positive maximum encoded result-evidence size.</param>
    /// <param name="maximumBatchSize">The positive maximum ordered reservation count in one atomic batch.</param>
    /// <param name="maximumLimitsPerScope">The positive maximum configured limit count in one scope.</param>
    /// <param name="maximumLineageDepth">The positive maximum scope ancestry depth.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lockTimeout"/> is not a positive whole-second duration representable by an <see cref="int"/>, or another bound is not positive.</exception>
    public SqliteBudgetLedgerSettings(
        TimeSpan lockTimeout,
        int maximumPayloadBytes,
        int maximumResultBytes,
        int maximumBatchSize,
        int maximumLimitsPerScope,
        int maximumLineageDepth)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lockTimeout, TimeSpan.FromSeconds(1));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lockTimeout.TotalSeconds, int.MaxValue, nameof(lockTimeout));
        ArgumentOutOfRangeException.ThrowIfNotEqual(
            lockTimeout.Ticks % TimeSpan.TicksPerSecond, 0, nameof(lockTimeout));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPayloadBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResultBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBatchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLimitsPerScope);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLineageDepth);

        LockTimeout = lockTimeout;
        MaximumPayloadBytes = maximumPayloadBytes;
        MaximumResultBytes = maximumResultBytes;
        MaximumBatchSize = maximumBatchSize;
        MaximumLimitsPerScope = maximumLimitsPerScope;
        MaximumLineageDepth = maximumLineageDepth;
    }

    /// <summary>Gets the finite provider lock-wait bound.</summary>
    /// <value>A positive whole-second duration used as SQLite's default and command timeout.</value>
    public TimeSpan LockTimeout { get; }
    /// <summary>Gets the maximum encoded immutable request-evidence size.</summary>
    /// <value>A positive byte count enforced before an encoded request is written and before a stored request is retained or decoded. Exact replay lookup may read its already bounded persisted envelope first.</value>
    public int MaximumPayloadBytes { get; }
    /// <summary>Gets the maximum encoded result-evidence size.</summary>
    /// <value>A positive byte count enforced before an encoded result is written and before a stored result is retained or decoded.</value>
    public int MaximumResultBytes { get; }
    /// <summary>Gets the maximum ordered resource count.</summary>
    /// <value>A positive count enforced for atomic reservation batches.</value>
    public int MaximumBatchSize { get; }
    /// <summary>Gets the maximum configured limit count per scope.</summary>
    /// <value>A positive count enforced before a scope is persisted.</value>
    public int MaximumLimitsPerScope { get; }
    /// <summary>Gets the maximum lineage length.</summary>
    /// <value>A positive depth enforced for each persisted scope lineage.</value>
    public int MaximumLineageDepth { get; }

    /// <summary>Creates conservative explicit defaults for local applications.</summary>
    /// <returns>Settings with a five-second lock timeout and bounded one-megabyte evidence payloads.</returns>
    public static SqliteBudgetLedgerSettings CreateDefault() => new(
        TimeSpan.FromSeconds(5),
        1_048_576,
        1_048_576,
        256,
        256,
        32);
}
