// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Sqlite;

/// <summary>Is the validated, immutable snapshot of a SQLite goal store's lock and size bounds.</summary>
public sealed record SqliteGoalStoreSettings
{
    /// <summary>Initializes validated settings.</summary>
    /// <param name="lockTimeout">The whole-second wait for another writer, at least one second.</param>
    /// <param name="maximumRecordBytes">The positive largest encoded goal document.</param>
    /// <param name="authorizedIntentScanners">The scanner identities allowed to discover intents.</param>
    /// <exception cref="ArgumentOutOfRangeException">The timeout is shorter than one second, not whole seconds, or too large, or the size bound is not positive.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="authorizedIntentScanners"/> is null.</exception>
    public SqliteGoalStoreSettings(TimeSpan lockTimeout, int maximumRecordBytes, IEnumerable<ComponentId> authorizedIntentScanners)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lockTimeout, TimeSpan.FromSeconds(1));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lockTimeout.TotalSeconds, int.MaxValue, nameof(lockTimeout));
        ArgumentOutOfRangeException.ThrowIfNotEqual(lockTimeout.Ticks % TimeSpan.TicksPerSecond, 0, nameof(lockTimeout));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRecordBytes);
        ArgumentNullException.ThrowIfNull(authorizedIntentScanners);
        LockTimeout = lockTimeout;
        MaximumRecordBytes = maximumRecordBytes;
        AuthorizedIntentScanners = [.. authorizedIntentScanners];
    }

    /// <summary>Gets the whole-second wait for another writer.</summary>
    public TimeSpan LockTimeout { get; }

    /// <summary>Gets the largest encoded goal document.</summary>
    public int MaximumRecordBytes { get; }

    /// <summary>Gets the scanner identities allowed to discover intents.</summary>
    public ImmutableArray<ComponentId> AuthorizedIntentScanners { get; }

    /// <summary>Creates settings with every documented default and no authorized scanner.</summary>
    /// <returns>Default settings.</returns>
    public static SqliteGoalStoreSettings CreateDefault() => new(TimeSpan.FromSeconds(5), 1_048_576, []);
}
