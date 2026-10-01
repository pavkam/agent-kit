// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>The published revision of one <see cref="GoalProfileKey"/>'s validated configuration, captured on durable goals and delegation requests.</summary>
/// <remarks>Goal configuration is captured, not monitored: a profile change produces a new version through validated publication, and resume or delayed joins use the version persisted on the record rather than the agent's latest definition. This immutable value has structural equality over <see cref="Value"/> and is safe to share across threads.</remarks>
public readonly record struct GoalProfileVersion
{
    /// <summary>Initializes a validated profile revision.</summary>
    /// <param name="value">The non-negative revision number. A published profile uses a positive value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public GoalProfileVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>Gets the profile revision number.</summary>
    public long Value { get; }

    /// <summary>Returns the revision number as invariant-culture text.</summary>
    /// <returns>The decimal revision number.</returns>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
