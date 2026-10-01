// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Sqlite;

/// <summary>Is the validated, immutable snapshot of a SQLite artifact store's lock and size bounds.</summary>
public sealed record SqliteArtifactSettings
{
    /// <summary>Initializes validated settings.</summary>
    /// <param name="lockTimeout">The whole-second wait for another process to release the database, at least one second.</param>
    /// <param name="maximumRecordBytes">The positive largest encoded entry document.</param>
    /// <exception cref="ArgumentOutOfRangeException">The timeout is shorter than one second, not whole seconds, or too large, or the size bound is not positive.</exception>
    public SqliteArtifactSettings(TimeSpan lockTimeout, int maximumRecordBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lockTimeout, TimeSpan.FromSeconds(1));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lockTimeout.TotalSeconds, int.MaxValue, nameof(lockTimeout));
        ArgumentOutOfRangeException.ThrowIfNotEqual(lockTimeout.Ticks % TimeSpan.TicksPerSecond, 0, nameof(lockTimeout));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRecordBytes);
        LockTimeout = lockTimeout;
        MaximumRecordBytes = maximumRecordBytes;
    }

    /// <summary>Gets the whole-second wait for another process to release the database.</summary>
    public TimeSpan LockTimeout { get; }

    /// <summary>Gets the largest encoded entry document, in bytes.</summary>
    public int MaximumRecordBytes { get; }

    /// <summary>Creates settings with every documented default.</summary>
    /// <returns>Default settings: a five-second lock wait and one-mebibyte entry documents.</returns>
    public static SqliteArtifactSettings CreateDefault() => new(TimeSpan.FromSeconds(5), 1_048_576);
}
