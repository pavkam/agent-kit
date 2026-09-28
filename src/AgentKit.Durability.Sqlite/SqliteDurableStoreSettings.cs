// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite;

/// <summary>Captures immutable positive operational and evidence bounds for one SQLite durable-store singleton.</summary>
/// <remarks>
/// The record bound applies to the encoded projection of one whole operation, which carries its declaration, latest
/// checkpoint, and terminal output together. A composition that persists large durable state raises this bound
/// deliberately rather than discovering the limit mid-run, because the bound is enforced before the write is attempted.
/// </remarks>
public sealed record SqliteDurableStoreSettings
{
    /// <summary>Initializes explicit lock-wait and encoded-record bounds.</summary>
    /// <param name="lockTimeout">The positive whole-second SQLite busy/locked wait bound representable by the provider.</param>
    /// <param name="maximumRecordBytes">The positive maximum encoded operation-projection or lease payload size.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lockTimeout"/> is not a positive whole-second duration representable by an <see cref="int"/>, or <paramref name="maximumRecordBytes"/> is not positive.</exception>
    public SqliteDurableStoreSettings(TimeSpan lockTimeout, int maximumRecordBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lockTimeout, TimeSpan.FromSeconds(1));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lockTimeout.TotalSeconds, int.MaxValue, nameof(lockTimeout));
        ArgumentOutOfRangeException.ThrowIfNotEqual(
            lockTimeout.Ticks % TimeSpan.TicksPerSecond, 0, nameof(lockTimeout));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRecordBytes);

        LockTimeout = lockTimeout;
        MaximumRecordBytes = maximumRecordBytes;
    }

    /// <summary>Gets the finite provider lock-wait bound.</summary>
    /// <value>A positive whole-second duration used as SQLite's default and command timeout.</value>
    public TimeSpan LockTimeout { get; }

    /// <summary>Gets the maximum encoded operation-projection or lease payload size.</summary>
    /// <value>A positive byte count enforced before database access and while decoding.</value>
    public int MaximumRecordBytes { get; }

    /// <summary>Creates conservative explicit defaults for local applications.</summary>
    /// <returns>Settings with a five-second lock timeout and a one-mebibyte record payload bound.</returns>
    public static SqliteDurableStoreSettings CreateDefault() => new(TimeSpan.FromSeconds(5), 1_048_576);
}
