// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Supplies the immutable projection policy resolved under its exact retained reference.</summary>
/// <remarks>The caller must still enforce the snapshot's bounds and allowed transformations before publishing content.</remarks>
public sealed record ToolResultProjectionPolicyResolved: ToolResultProjectionPolicyResolution
{
    /// <summary>Initializes a successful lookup from the retained immutable policy snapshot.</summary>
    /// <param name="snapshot">The nonnull snapshot whose reference was requested from the catalog.</param>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
    public ToolResultProjectionPolicyResolved(ToolResultProjectionPolicySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Snapshot = snapshot;
    }

    /// <summary>Gets the resolved immutable policy content.</summary>
    /// <value>The retained snapshot; it owns no disposable resource and may be shared across concurrent readers.</value>
    public ToolResultProjectionPolicySnapshot Snapshot { get; }

    /// <inheritdoc/>
    public override ToolResultProjectionPolicyReference Reference => Snapshot.Reference;
}
