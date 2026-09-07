// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one human question across publication, response, and tool settlement.</summary>
public readonly record struct QuestionId
{
    /// <summary>Initializes a non-empty question identity.</summary>
    /// <param name="value">The globally unique question value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    public QuestionId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>Gets the globally unique question value.</summary>
    public Guid Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D");
}
