// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Captures finite SQLite lock, serialization, and continuation bounds.</summary>
/// <remarks>
/// Instances are immutable and validated on construction, so one settings
/// record can be handed to the store and directory without further checks.
/// <see cref="CreateDefault"/> and the mutable
/// <see cref="SqliteSessionStoreOptions"/> agree on the same default values.
/// </remarks>
public sealed record SqliteSessionStoreSettings
{
    /// <summary>Initializes explicit operational bounds.</summary>
    /// <param name="lockTimeout">A positive whole-second provider lock timeout of at least one second.</param>
    /// <param name="maximumEntryPayloadBytes">The positive maximum encoded byte length of one committed entry payload.</param>
    /// <param name="maximumIssuedReadSnapshots">
    /// The positive number of distinct adapter-issued read snapshots retained in
    /// process for exact paged continuation. Defaults to 4,096.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="lockTimeout"/> is shorter than one second, is not a whole
    /// number of seconds, or exceeds <see cref="int.MaxValue"/> seconds;
    /// <paramref name="maximumEntryPayloadBytes"/> is zero or negative; or
    /// <paramref name="maximumIssuedReadSnapshots"/> is zero or negative.
    /// </exception>
    public SqliteSessionStoreSettings(TimeSpan lockTimeout, int maximumEntryPayloadBytes, int maximumIssuedReadSnapshots = 4096)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lockTimeout, TimeSpan.FromSeconds(1));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lockTimeout.TotalSeconds, int.MaxValue, nameof(lockTimeout));
        ArgumentOutOfRangeException.ThrowIfNotEqual(lockTimeout.Ticks % TimeSpan.TicksPerSecond, 0, nameof(lockTimeout));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEntryPayloadBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumIssuedReadSnapshots);
        LockTimeout = lockTimeout;
        MaximumEntryPayloadBytes = maximumEntryPayloadBytes;
        MaximumIssuedReadSnapshots = maximumIssuedReadSnapshots;
    }

    /// <summary>Gets the SQLite busy timeout.</summary><value>A positive whole-second duration.</value>
    public TimeSpan LockTimeout { get; }
    /// <summary>Gets the bound on one committed entry's encoded payload.</summary>
    /// <value>A positive byte count; an operation committing an entry that exceeds it is rejected with a typed failure before any write.</value>
    public int MaximumEntryPayloadBytes { get; }
    /// <summary>Gets how many adapter-issued paged-read snapshots one store instance retains for exact continuation.</summary>
    /// <value>A positive count; once exceeded, the oldest snapshot is evicted and continuing from it fails.</value>
    public int MaximumIssuedReadSnapshots { get; }
    /// <summary>Creates conservative local defaults.</summary>
    /// <returns>A five-second timeout, a one-mebibyte payload bound, and 4,096 retained read snapshots.</returns>
    public static SqliteSessionStoreSettings CreateDefault() => new(TimeSpan.FromSeconds(5), 1_048_576, 4096);
}
