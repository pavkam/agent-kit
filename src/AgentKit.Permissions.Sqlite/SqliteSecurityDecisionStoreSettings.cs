// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite;

/// <summary>Captures immutable positive operational and evidence bounds for one SQLite decision-store singleton.</summary>
public sealed record SqliteSecurityDecisionStoreSettings
{
    /// <summary>Initializes explicit lock-wait and encoded-decision bounds.</summary>
    /// <param name="lockTimeout">The positive whole-second SQLite busy/locked wait bound representable by the provider.</param>
    /// <param name="maximumDecisionBytes">The positive maximum encoded terminal decision size.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lockTimeout"/> is not a positive whole-second duration representable by an <see cref="int"/>, or <paramref name="maximumDecisionBytes"/> is not positive.</exception>
    public SqliteSecurityDecisionStoreSettings(TimeSpan lockTimeout, int maximumDecisionBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lockTimeout, TimeSpan.FromSeconds(1));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lockTimeout.TotalSeconds, int.MaxValue, nameof(lockTimeout));
        ArgumentOutOfRangeException.ThrowIfNotEqual(
            lockTimeout.Ticks % TimeSpan.TicksPerSecond, 0, nameof(lockTimeout));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDecisionBytes);

        LockTimeout = lockTimeout;
        MaximumDecisionBytes = maximumDecisionBytes;
    }

    /// <summary>Gets the finite provider lock-wait bound.</summary>
    /// <value>A positive whole-second duration used as SQLite's default and command timeout.</value>
    public TimeSpan LockTimeout { get; }

    /// <summary>Gets the maximum encoded terminal decision size.</summary>
    /// <value>A positive byte count enforced before database access and while decoding.</value>
    public int MaximumDecisionBytes { get; }

    /// <summary>Creates conservative explicit defaults for local applications.</summary>
    /// <returns>Settings with a five-second lock timeout and a one-mebibyte decision payload bound.</returns>
    public static SqliteSecurityDecisionStoreSettings CreateDefault() => new(TimeSpan.FromSeconds(5), 1_048_576);
}
