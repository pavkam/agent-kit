// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies one positive configuration-source publication revision.</summary>
/// <remarks>The default value has revision zero and is uninitialized.</remarks>
public readonly record struct ConfigurationSourceVersion
{
    /// <summary>Initializes a positive source revision.</summary>
    /// <param name="value">Positive published revision.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is zero or negative.</exception>
    public ConfigurationSourceVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets publication revision.</summary>
    /// <value>A positive value, or zero only for the uninitialized default.</value>
    public long Value { get; }

    /// <summary>Formats the revision invariantly.</summary>
    /// <returns>Invariant decimal text, including <c>0</c> for default.</returns>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
