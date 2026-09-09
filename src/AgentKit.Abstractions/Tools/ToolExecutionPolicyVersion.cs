// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies one positive immutable tool-execution policy revision.</summary>
/// <remarks>Changing execution behavior publishes a new version rather than mutating captured policy evidence.</remarks>
public readonly record struct ToolExecutionPolicyVersion
{
    /// <summary>Initializes a tool-execution policy version.</summary>
    /// <param name="value">The positive policy revision.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is zero or negative.</exception>
    public ToolExecutionPolicyVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the positive policy revision.</summary>
    /// <value>A positive integer, or zero only for an uninitialized default value.</value>
    public long Value { get; }

    /// <summary>Returns the policy revision using invariant culture.</summary>
    /// <returns>The decimal revision text.</returns>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
