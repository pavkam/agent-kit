// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one bounded security grant and its authoritative use state.</summary>
public readonly record struct GrantId
{
    /// <summary>Initializes a validated grant identifier.</summary>
    /// <param name="value">The non-empty globally unique value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    public GrantId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>Gets the globally unique value.</summary>
    public Guid Value { get; }

    /// <summary>Returns the canonical lowercase identifier text.</summary>
    public override string ToString() => Value.ToString("D");
}
