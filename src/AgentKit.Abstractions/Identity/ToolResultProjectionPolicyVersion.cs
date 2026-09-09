// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies one positive, monotonically published projection-policy revision.</summary>
public readonly record struct ToolResultProjectionPolicyVersion
{
    /// <summary>Initializes a projection-policy revision.</summary>
    /// <param name="value">The positive revision value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is zero or negative.</exception>
    public ToolResultProjectionPolicyVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the positive revision value.</summary>
    /// <value>A value greater than zero.</value>
    public long Value { get; }

    /// <summary>Returns the revision using invariant-culture decimal text.</summary>
    /// <returns>The invariant representation of <see cref="Value"/>.</returns>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
