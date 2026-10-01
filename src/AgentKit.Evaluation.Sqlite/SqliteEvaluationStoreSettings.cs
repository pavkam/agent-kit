// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Sqlite;

/// <summary>Is the validated, immutable snapshot of a SQLite evaluation result store lock and size bounds.</summary>
public sealed record SqliteEvaluationStoreSettings
{
    /// <summary>Initializes validated settings.</summary>
    /// <param name="lockTimeout">The whole-second wait for another writer, at least one second.</param>
    /// <param name="maximumRecordBytes">The positive largest encoded result document.</param>
    /// <exception cref="ArgumentOutOfRangeException">The timeout is shorter than one second, not whole seconds, or too large, or the size bound is not positive.</exception>
    public SqliteEvaluationStoreSettings(TimeSpan lockTimeout, int maximumRecordBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lockTimeout, TimeSpan.FromSeconds(1));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lockTimeout.TotalSeconds, int.MaxValue, nameof(lockTimeout));
        ArgumentOutOfRangeException.ThrowIfNotEqual(lockTimeout.Ticks % TimeSpan.TicksPerSecond, 0, nameof(lockTimeout));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRecordBytes);
        LockTimeout = lockTimeout;
        MaximumRecordBytes = maximumRecordBytes;
    }

    /// <summary>Gets the whole-second wait for another writer.</summary>
    public TimeSpan LockTimeout { get; }

    /// <summary>Gets the largest encoded result document.</summary>
    public int MaximumRecordBytes { get; }

    /// <summary>Creates settings with every documented default.</summary>
    /// <returns>Default settings.</returns>
    public static SqliteEvaluationStoreSettings CreateDefault() => new(TimeSpan.FromSeconds(5), 1_048_576);
}
