// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite;

/// <summary>Captures immutable positive operational and evidence bounds for one SQLite grant-store singleton.</summary>
public sealed record SqliteSecurityGrantStoreSettings
{
    /// <summary>Initializes explicit lock-wait and evidence bounds.</summary>
    /// <param name="lockTimeout">The positive whole-second SQLite busy/locked wait bound representable by the provider.</param>
    /// <param name="maximumGrantBytes">The positive maximum encoded immutable grant-evidence size.</param>
    /// <param name="maximumEnforcementBytes">The positive maximum encoded enforcement-evidence size.</param>
    /// <param name="maximumResources">The positive maximum ordered resource count in a grant or enforcement request.</param>
    /// <param name="maximumClaims">The positive maximum claims in an identity or one delegation link.</param>
    /// <param name="maximumDelegationLinks">The positive maximum identity delegation depth.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lockTimeout"/> is not a positive whole-second duration representable by an <see cref="int"/>, or another bound is not positive.</exception>
    public SqliteSecurityGrantStoreSettings(
        TimeSpan lockTimeout,
        int maximumGrantBytes,
        int maximumEnforcementBytes,
        int maximumResources,
        int maximumClaims,
        int maximumDelegationLinks)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lockTimeout, TimeSpan.FromSeconds(1));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lockTimeout.TotalSeconds, int.MaxValue, nameof(lockTimeout));
        ArgumentOutOfRangeException.ThrowIfNotEqual(
            lockTimeout.Ticks % TimeSpan.TicksPerSecond, 0, nameof(lockTimeout));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumGrantBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEnforcementBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResources);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumClaims);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDelegationLinks);

        LockTimeout = lockTimeout;
        MaximumGrantBytes = maximumGrantBytes;
        MaximumEnforcementBytes = maximumEnforcementBytes;
        MaximumResources = maximumResources;
        MaximumClaims = maximumClaims;
        MaximumDelegationLinks = maximumDelegationLinks;
    }

    /// <summary>Gets the finite provider lock-wait bound.</summary>
    /// <value>A positive whole-second duration used as SQLite's default and command timeout.</value>
    public TimeSpan LockTimeout { get; }
    /// <summary>Gets the maximum encoded immutable grant size.</summary>
    /// <value>A positive byte count enforced before database access and while decoding.</value>
    public int MaximumGrantBytes { get; }
    /// <summary>Gets the maximum encoded enforcement size.</summary>
    /// <value>A positive byte count enforced before database access and while decoding.</value>
    public int MaximumEnforcementBytes { get; }
    /// <summary>Gets the maximum ordered resource count.</summary>
    /// <value>A positive count enforced for grants, enforcement requests, and receipts.</value>
    public int MaximumResources { get; }
    /// <summary>Gets the maximum ordered claim count.</summary>
    /// <value>A positive count enforced in each identity claim collection.</value>
    public int MaximumClaims { get; }
    /// <summary>Gets the maximum delegation-chain length.</summary>
    /// <value>A positive depth enforced for each encoded identity.</value>
    public int MaximumDelegationLinks { get; }

    /// <summary>Creates conservative explicit defaults for local applications.</summary>
    /// <returns>Settings with a five-second lock timeout and bounded one-megabyte evidence payloads.</returns>
    public static SqliteSecurityGrantStoreSettings CreateDefault() => new(
        TimeSpan.FromSeconds(5),
        1_048_576,
        1_048_576,
        256,
        256,
        32);
}
