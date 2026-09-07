// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one mutually exclusive answer option within a human question.</summary>
public readonly record struct QuestionOptionId
{
    /// <summary>Initializes a non-blank option identity.</summary>
    /// <param name="value">The stable option text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is blank.</exception>
    public QuestionOptionId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the stable option text.</summary>
    public string Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
