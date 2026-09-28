// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite;

/// <summary>Identifies the exact initialized SQLite durable-store target expected by host bootstrap configuration.</summary>
/// <remarks>
/// The journal and the lease manager share one database, so they also share one store identity. Verifying it on every
/// connection is what turns an accidentally repointed path into an immediate failure instead of a journal that answers
/// recovery questions about a different deployment's operations.
/// </remarks>
public readonly record struct SqliteDurableStoreInstanceId
{
    /// <summary>Initializes a nondefault store-instance identity.</summary>
    /// <param name="value">The globally unique store identity persisted in schema metadata.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    public SqliteDurableStoreInstanceId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>Gets the globally unique store identity.</summary>
    /// <value>The nonempty value verified on every database connection.</value>
    public Guid Value { get; }

    /// <summary>Returns the canonical lowercase identity text.</summary>
    /// <returns>The hyphenated identity used only for safe diagnostics.</returns>
    public override string ToString() => Value.ToString("D");
}
