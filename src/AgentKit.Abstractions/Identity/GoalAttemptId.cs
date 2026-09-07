// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one durable execution attempt for a goal.</summary>
public readonly record struct GoalAttemptId
{
    /// <summary>Initializes a non-empty attempt identity.</summary>
    /// <param name="value">The globally unique value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    public GoalAttemptId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>Gets the globally unique value.</summary>
    public Guid Value { get; }

    /// <summary>Returns the canonical text form.</summary>
    public override string ToString() => Value.ToString("D");
}
