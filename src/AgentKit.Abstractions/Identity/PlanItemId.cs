// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one stable item within a work plan.</summary>
public readonly record struct PlanItemId
{
    /// <summary>Initializes a non-blank item identity.</summary>
    /// <param name="value">The stable item text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is blank.</exception>
    public PlanItemId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the stable item text.</summary>
    public string Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
