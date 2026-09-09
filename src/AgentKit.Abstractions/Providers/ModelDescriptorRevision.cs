// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies one positive immutable model-descriptor publication revision.</summary>
/// <remarks>This is distinct from catalog and profile revisions and does not resolve or authorize a descriptor.</remarks>
public readonly record struct ModelDescriptorRevision
{
    /// <summary>Initializes a positive descriptor publication revision.</summary>
    /// <param name="value">The positive exact revision.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is zero or negative.</exception>
    public ModelDescriptorRevision(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the exact descriptor publication revision.</summary>
    /// <value>A positive number, or zero only for the CLR default value.</value>
    public long Value { get; }

    /// <summary>Formats the revision using invariant decimal digits.</summary>
    /// <returns>The invariant revision text.</returns>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
