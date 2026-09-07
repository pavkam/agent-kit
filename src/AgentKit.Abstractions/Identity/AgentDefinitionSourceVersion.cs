// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>
/// The revision of one agent-definition source's contributed content, used to
/// detect whether a refresh actually changed what that source publishes.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. It carries no mutable state and is safe to share and
/// compare across threads without synchronization.
/// </para>
/// <para>
/// Source versions are independent of <see cref="AgentCatalogVersion"/>. The
/// catalog version changes when the composed snapshot is republished; a source
/// version changes only when that one contributor's own content changes.
/// </para>
/// </remarks>
public readonly record struct AgentDefinitionSourceVersion
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="AgentDefinitionSourceVersion"/> struct, validating that it
    /// is not a nonsensical negative revision.
    /// </summary>
    /// <param name="value">The non-negative source revision number.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is negative.
    /// </exception>
    public AgentDefinitionSourceVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>Gets the source revision number.</summary>
    public long Value { get; }

    /// <summary>
    /// Returns the revision number as invariant-culture text, suitable for
    /// logging and refresh diagnostics.
    /// </summary>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
