// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>
/// The revision of one agent definition's declarative content, used to tell
/// two versions of the same <see cref="AgentId"/> apart.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. It carries no mutable state and is safe to share and
/// compare across threads without synchronization.
/// </para>
/// <para>
/// Definitions are immutable, so changing an agent's behavior publishes a new
/// revision rather than mutating the existing one. A run captures the revision
/// it started under, which is what lets a later investigation distinguish "the
/// agent behaved differently" from "the agent was reconfigured".
/// </para>
/// <para>
/// Revisions also decide precedence conflicts: when two definition sources
/// publish the same <see cref="AgentId"/>, the catalog uses source precedence
/// and this revision to reject or resolve the collision rather than silently
/// preferring whichever loaded last.
/// </para>
/// </remarks>
public readonly record struct AgentDefinitionRevision
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="AgentDefinitionRevision"/> struct, validating that it is not
    /// a nonsensical negative revision.
    /// </summary>
    /// <param name="value">The non-negative revision number.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is negative.
    /// </exception>
    public AgentDefinitionRevision(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>Gets the revision number.</summary>
    public long Value { get; }

    /// <summary>
    /// Returns the revision number as invariant-culture text, suitable for
    /// logging and catalog diagnostics.
    /// </summary>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
