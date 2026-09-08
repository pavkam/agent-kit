// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies one immutable, validated session-profile revision.</summary>
/// <remarks>The value is configuration evidence, not a mutable counter or routing authority. A profile snapshot retains it so an in-flight operation remains bound to its compiled session behavior.</remarks>
public readonly record struct SessionProfileVersion
{
    /// <summary>Initializes a positive session-profile revision.</summary>
    /// <param name="value">The positive immutable revision number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public SessionProfileVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the positive immutable revision number.</summary>
    /// <value>The configured revision, or zero only for an unvalidated default value.</value>
    public long Value { get; }

    /// <summary>Returns the invariant-culture revision text.</summary>
    /// <returns>The numeric revision formatted with invariant culture.</returns>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
