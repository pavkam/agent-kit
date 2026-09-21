// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite;

/// <summary>Identifies the exact initialized SQLite approval-store target expected by host bootstrap configuration.</summary>
public readonly record struct SqliteApprovalStoreInstanceId
{
    /// <summary>Initializes a nondefault store-instance identity.</summary>
    /// <param name="value">The globally unique store identity persisted in schema metadata.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    public SqliteApprovalStoreInstanceId(Guid value)
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
