// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies a positive immutable run-policy snapshot used for continuation evaluation.</summary>
/// <remarks>
/// Equality is value-based. The version identifies the policy rules captured in
/// a <see cref="RunContinuationContext"/> and supports revalidation; it is not
/// a mutable policy handle or an ordering guarantee across unrelated runs.
/// </remarks>
public readonly record struct RunPolicyVersion
{
    /// <summary>Initializes a policy-snapshot identity.</summary>
    /// <param name="value">The positive, policy-owner-assigned version number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is zero or negative.</exception>
    public RunPolicyVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the positive policy-owner-assigned version number.</summary>
    /// <value>A value greater than zero that participates in value equality and invariant text formatting.</value>
    public long Value { get; }

    /// <summary>Formats the numeric version using invariant culture.</summary>
    /// <returns>The decimal representation of <see cref="Value"/> using invariant culture.</returns>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
