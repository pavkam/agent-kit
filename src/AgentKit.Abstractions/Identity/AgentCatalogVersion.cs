// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>
/// The revision of one composed agent-definition catalog snapshot,
/// incremented whenever definition sources are recomposed and republished.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. It carries no mutable state and is safe to share and
/// compare across threads without synchronization.
/// </para>
/// <para>
/// A run records the catalog version it resolved its definition from, so a
/// dynamic reload that happens mid-run is distinguishable from a definition
/// that was already different when the run started.
/// </para>
/// </remarks>
public readonly record struct AgentCatalogVersion
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentCatalogVersion"/>
    /// struct, validating that it is not a nonsensical negative revision.
    /// </summary>
    /// <param name="value">The non-negative catalog revision number.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is negative.
    /// </exception>
    public AgentCatalogVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>Gets the catalog revision number.</summary>
    public long Value { get; }

    /// <summary>
    /// Returns the revision number as invariant-culture text, suitable for
    /// logging and resolution diagnostics.
    /// </summary>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
