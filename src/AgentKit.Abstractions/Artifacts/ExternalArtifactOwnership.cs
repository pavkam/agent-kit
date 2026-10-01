// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names content an external system owns, by a stable unsigned locator and an explicit delete delegation.</summary>
/// <remarks>AgentKit never claims durability, immutability, or deletion of content it does not own. A signed URL or current backend route is never the canonical locator.</remarks>
public sealed record ExternalArtifactOwnership
{
    /// <summary>Initializes external ownership evidence.</summary>
    /// <param name="resourceId">The external system's stable identity for the resource.</param>
    /// <param name="canonicalUri">The stable absolute locator, free of user information, query, and fragment.</param>
    /// <param name="agentKitMayDelete">Whether the external owner delegated delete authority to AgentKit.</param>
    /// <exception cref="ArgumentException"><paramref name="resourceId"/> is blank, or <paramref name="canonicalUri"/> is relative or carries user information, a query, or a fragment.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="canonicalUri"/> is null.</exception>
    public ExternalArtifactOwnership(ExternalArtifactResourceId resourceId, Uri canonicalUri, bool agentKitMayDelete)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId.Value, nameof(resourceId));
        ArgumentException.ThrowIfNotStableUnsignedUri(canonicalUri);
        ResourceId = resourceId;
        CanonicalUri = canonicalUri;
        AgentKitMayDelete = agentKitMayDelete;
    }

    /// <summary>Gets the external system's stable identity for the resource.</summary>
    public ExternalArtifactResourceId ResourceId { get; }

    /// <summary>Gets the stable unsigned absolute locator.</summary>
    public Uri CanonicalUri { get; }

    /// <summary>Gets whether the external owner delegated delete authority to AgentKit.</summary>
    public bool AgentKitMayDelete { get; }
}
