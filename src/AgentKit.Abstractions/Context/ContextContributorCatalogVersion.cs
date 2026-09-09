// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Identifies one positive immutable publication of the context-contributor catalog.</summary>
/// <remarks>The number identifies exact catalog evidence; it does not authorize or resolve contributors.</remarks>
public readonly record struct ContextContributorCatalogVersion
{
    /// <summary>Creates an exact positive contributor-catalog version.</summary>
    /// <param name="value">The positive publication number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is zero or negative.</exception>
    public ContextContributorCatalogVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the exact catalog publication number.</summary>
    /// <value>A positive number, or zero only for the CLR default value.</value>
    public long Value { get; }

    /// <summary>Formats the publication number independently of the current culture.</summary>
    /// <returns>The invariant decimal publication text.</returns>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
