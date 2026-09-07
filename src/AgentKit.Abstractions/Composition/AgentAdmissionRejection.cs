// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes why a pinned agent handle cannot start new work.</summary>
/// <remarks>
/// The value contains only catalog identity and a content-free reason. It is
/// created before a run scope or <see cref="RunId"/> exists.
/// </remarks>
public sealed record AgentAdmissionRejection
{
    /// <summary>Initializes immutable evidence for a rejected admission.</summary>
    /// <param name="agentId">The non-default identity requested through the pinned handle.</param>
    /// <param name="pinnedRevision">The revision captured when the handle was resolved.</param>
    /// <param name="catalogVersion">The current catalog snapshot examined at admission.</param>
    /// <param name="reason">A non-empty, content-free explanation of the unavailable definition.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="agentId"/> is the default identity.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="reason"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is empty or whitespace-only.</exception>
    public AgentAdmissionRejection(
        AgentId agentId,
        AgentDefinitionRevision pinnedRevision,
        AgentCatalogVersion catalogVersion,
        string reason)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        AgentId = agentId;
        PinnedRevision = pinnedRevision;
        CatalogVersion = catalogVersion;
        Reason = reason;
    }

    /// <summary>Gets the identity requested through the pinned handle.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the revision captured when the handle was resolved.</summary>
    public AgentDefinitionRevision PinnedRevision { get; }

    /// <summary>Gets the current catalog snapshot examined at admission.</summary>
    public AgentCatalogVersion CatalogVersion { get; }

    /// <summary>Gets the content-free explanation of the unavailable definition.</summary>
    public string Reason { get; }
}
