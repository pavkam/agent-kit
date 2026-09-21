// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite;

/// <summary>Mutable composition-time input for the operational and evidence bounds of one SQLite decision-store leaf.</summary>
/// <remarks>
/// Instances exist only while a configure delegate passed to
/// <see cref="ServiceExtensions.AddSqliteSecurityDecisionStore(IServiceCollection, SqliteSecurityDecisionStoreTarget, Action{SqliteSecurityDecisionStoreOptions}?)"/>
/// runs. The registration materializes the mutated values into an immutable, eagerly validated
/// <see cref="SqliteSecurityDecisionStoreSettings"/> and never registers this type in dependency injection.
/// </remarks>
public sealed class SqliteSecurityDecisionStoreOptions
{
    /// <summary>Gets or sets the finite provider lock-wait bound.</summary>
    /// <value>A positive whole-second duration. Defaults to five seconds.</value>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets the maximum encoded terminal decision size.</summary>
    /// <value>A positive byte count. Defaults to one mebibyte.</value>
    public int MaximumDecisionBytes { get; set; } = 1_048_576;
}
