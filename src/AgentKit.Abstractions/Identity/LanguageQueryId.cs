// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one bounded language-intelligence query across authorization and execution.</summary>
public readonly record struct LanguageQueryId
{
    /// <summary>Initializes a non-empty query identity.</summary>
    /// <param name="value">The globally unique query value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    public LanguageQueryId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>Gets the globally unique query value.</summary>
    public Guid Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D");
}
