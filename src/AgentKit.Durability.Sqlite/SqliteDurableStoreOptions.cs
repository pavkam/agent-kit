// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite;

/// <summary>Mutable composition-time input for the operational and evidence bounds of one SQLite durable-store leaf.</summary>
/// <remarks>
/// Instances exist only while a configure delegate passed to a registration extension runs. The registration
/// materializes the mutated values into an immutable, eagerly validated <see cref="SqliteDurableStoreSettings"/> and
/// never registers this type in dependency injection.
/// </remarks>
public sealed class SqliteDurableStoreOptions
{
    /// <summary>Gets or sets the finite provider lock-wait bound.</summary>
    /// <value>A positive whole-second duration. Defaults to five seconds.</value>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets the maximum encoded operation-projection or lease payload size.</summary>
    /// <value>A positive byte count. Defaults to one mebibyte.</value>
    public int MaximumRecordBytes { get; set; } = 1_048_576;
}
