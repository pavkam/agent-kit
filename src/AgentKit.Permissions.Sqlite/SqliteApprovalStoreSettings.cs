// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite;

/// <summary>Captures immutable positive operational and evidence bounds for one SQLite approval-store singleton.</summary>
public sealed record SqliteApprovalStoreSettings
{
    /// <summary>Initializes explicit lock-wait and encoded-record bounds.</summary>
    /// <param name="lockTimeout">The positive whole-second SQLite busy/locked wait bound representable by the provider.</param>
    /// <param name="maximumRecordBytes">The positive maximum encoded request or response payload size.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lockTimeout"/> is not a positive whole-second duration representable by an <see cref="int"/>, or <paramref name="maximumRecordBytes"/> is not positive.</exception>
    public SqliteApprovalStoreSettings(TimeSpan lockTimeout, int maximumRecordBytes)
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

    /// <summary>Gets the maximum encoded request or response payload size.</summary>
    /// <value>A positive byte count enforced before database access and while decoding.</value>
    public int MaximumRecordBytes { get; }

    /// <summary>Creates conservative explicit defaults for local applications.</summary>
    /// <returns>Settings with a five-second lock timeout and a one-mebibyte record payload bound.</returns>
    public static SqliteApprovalStoreSettings CreateDefault() => new(TimeSpan.FromSeconds(5), 1_048_576);
}
