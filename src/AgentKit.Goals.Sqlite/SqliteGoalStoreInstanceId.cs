// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Sqlite;

/// <summary>Identifies one SQLite goal database across restarts, so it is never opened under the wrong configuration.</summary>
public readonly record struct SqliteGoalStoreInstanceId
{
    /// <summary>Initializes a non-empty instance identity.</summary>
    /// <param name="value">The globally unique value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    public SqliteGoalStoreInstanceId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>Gets the globally unique value.</summary>
    public Guid Value { get; }

    /// <summary>Returns the canonical text form.</summary>
    /// <returns>The value in hyphenated form.</returns>
    public override string ToString() => Value.ToString("D");
}
