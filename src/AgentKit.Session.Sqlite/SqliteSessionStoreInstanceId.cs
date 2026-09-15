// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Identifies one exact initialized SQLite session database.</summary>
public readonly record struct SqliteSessionStoreInstanceId
{
    /// <summary>Initializes a persistent store identity.</summary>
    /// <param name="value">The nonempty identity.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    public SqliteSessionStoreInstanceId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>Gets the persistent identity.</summary>
    /// <value>A nonempty GUID.</value>
    public Guid Value { get; }
}
