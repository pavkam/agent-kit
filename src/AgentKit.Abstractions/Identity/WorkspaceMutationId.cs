// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one exact planned workspace mutation and its private staging resources.</summary>
public readonly record struct WorkspaceMutationId
{
    /// <summary>Initializes a mutation identity.</summary>
    /// <param name="value">The non-empty identity value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    public WorkspaceMutationId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>Gets the identity value.</summary>
    public Guid Value { get; }

    /// <summary>Returns the canonical lowercase identity.</summary>
    public override string ToString() => Value.ToString("D");
}
