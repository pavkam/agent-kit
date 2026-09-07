// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>
/// The revision of one composed model catalog snapshot, incremented whenever
/// descriptor sources are recomposed into a new published snapshot.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. It carries no mutable state and is safe to share and
/// compare across threads without synchronization.
/// </para>
/// <para>
/// The version is recorded on every selection decision so that a later
/// diagnosis can tell whether two runs saw the same configured models. A
/// catalog that reloads while runs are in flight publishes a new version
/// rather than mutating the snapshot an in-flight operation already read.
/// </para>
/// </remarks>
public readonly record struct ModelCatalogVersion
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ModelCatalogVersion"/>
    /// struct, validating that it is not a nonsensical negative revision.
    /// </summary>
    /// <param name="value">The non-negative catalog revision number.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is negative.
    /// </exception>
    public ModelCatalogVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>Gets the catalog revision number.</summary>
    public long Value { get; }

    /// <summary>
    /// Returns the revision number as invariant-culture text, suitable for
    /// logging and selection diagnostics.
    /// </summary>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
