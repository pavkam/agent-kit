// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines the closed resolved and unavailable outcomes of exact projection-policy lookup.</summary>
/// <remarks>Neither outcome grants authority, transforms content, or permits replacement of the requested version.</remarks>
public abstract record ToolResultProjectionPolicyResolution
{
    /// <summary>Initializes one of the two supported resolution outcomes.</summary>
    /// <exception cref="ArgumentException">The constructed runtime type is outside the closed resolution family.</exception>
    private protected ToolResultProjectionPolicyResolution() =>
        ArgumentException.ThrowIfNotEqual(this is ToolResultProjectionPolicyResolved or ToolResultProjectionPolicyUnavailable, true, "resolution");

    /// <summary>Copies the base state of a supported immutable resolution outcome.</summary>
    /// <param name="original">The nonnull original resolution.</param>
    /// <exception cref="ArgumentNullException"><paramref name="original"/> is null.</exception>
    /// <exception cref="ArgumentException">The constructed runtime type is outside the closed resolution family.</exception>
    protected ToolResultProjectionPolicyResolution(ToolResultProjectionPolicyResolution original)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentException.ThrowIfNotEqual(this is ToolResultProjectionPolicyResolved or ToolResultProjectionPolicyUnavailable, true, "resolution");
    }

    /// <summary>Gets the exact captured reference requested from the catalog.</summary>
    /// <value>The immutable key and positive revision, retained even when the snapshot is unavailable.</value>
    public abstract ToolResultProjectionPolicyReference Reference { get; }
}
