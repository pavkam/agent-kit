// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Captures finite SQLite lock and serialization bounds.</summary>
public sealed record SqliteSessionStoreSettings
{
    /// <summary>Initializes explicit operational bounds.</summary>
    /// <param name="lockTimeout">A positive whole-second provider lock timeout.</param>
    /// <param name="maximumEntryPayloadBytes">The positive maximum encoded entry payload.</param>
    /// <exception cref="ArgumentOutOfRangeException">A bound is invalid.</exception>
    public SqliteSessionStoreSettings(TimeSpan lockTimeout, int maximumEntryPayloadBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lockTimeout, TimeSpan.FromSeconds(1));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lockTimeout.TotalSeconds, int.MaxValue, nameof(lockTimeout));
        ArgumentOutOfRangeException.ThrowIfNotEqual(lockTimeout.Ticks % TimeSpan.TicksPerSecond, 0, nameof(lockTimeout));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEntryPayloadBytes);
        LockTimeout = lockTimeout;
        MaximumEntryPayloadBytes = maximumEntryPayloadBytes;
    }

    /// <summary>Gets the SQLite busy timeout.</summary><value>A positive whole-second duration.</value>
    public TimeSpan LockTimeout { get; }
    /// <summary>Gets the entry payload bound.</summary><value>A positive byte count.</value>
    public int MaximumEntryPayloadBytes { get; }
    /// <summary>Creates conservative local defaults.</summary><returns>A five-second timeout and one-mebibyte payload bound.</returns>
    public static SqliteSessionStoreSettings CreateDefault() => new(TimeSpan.FromSeconds(5), 1_048_576);
}
