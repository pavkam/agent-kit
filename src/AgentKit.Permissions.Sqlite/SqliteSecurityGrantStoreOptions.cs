// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite;

/// <summary>Mutable composition-time input for the operational and evidence bounds of one SQLite grant-store leaf.</summary>
/// <remarks>
/// Instances exist only while a configure delegate passed to
/// <see cref="ServiceExtensions.AddSqliteSecurityGrantStore(IServiceCollection, SqliteSecurityGrantStoreTarget, Action{SqliteSecurityGrantStoreOptions}?)"/>
/// runs. The registration materializes the mutated values into an immutable, eagerly validated
/// <see cref="SqliteSecurityGrantStoreSettings"/> and never registers this type in dependency injection, so the
/// captured store bounds cannot drift after composition. Every property defaults to the value produced by
/// <see cref="SqliteSecurityGrantStoreSettings.CreateDefault"/>. The type performs no validation of its own; invalid
/// values are rejected by the settings constructor with <see cref="ArgumentOutOfRangeException"/> at registration.
/// </remarks>
public sealed class SqliteSecurityGrantStoreOptions
{
    /// <summary>Gets or sets the finite provider lock-wait bound.</summary>
    /// <value>
    /// A positive whole-second duration representable by an <see cref="int"/> second count, used as SQLite's default
    /// and command timeout. Defaults to five seconds.
    /// </value>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets the maximum encoded immutable grant size.</summary>
    /// <value>A positive byte count enforced before database access and while decoding. Defaults to one mebibyte.</value>
    public int MaximumGrantBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets the maximum encoded enforcement size.</summary>
    /// <value>A positive byte count enforced before database access and while decoding. Defaults to one mebibyte.</value>
    public int MaximumEnforcementBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets the maximum ordered resource count.</summary>
    /// <value>A positive count enforced for grants, enforcement requests, and receipts. Defaults to 256.</value>
    public int MaximumResources { get; set; } = 256;

    /// <summary>Gets or sets the maximum ordered claim count.</summary>
    /// <value>A positive count enforced in each identity claim collection. Defaults to 256.</value>
    public int MaximumClaims { get; set; } = 256;

    /// <summary>Gets or sets the maximum delegation-chain length.</summary>
    /// <value>A positive depth enforced for each encoded identity. Defaults to 32.</value>
    public int MaximumDelegationLinks { get; set; } = 32;
}
