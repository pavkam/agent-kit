// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries exact read authority to the backend owning committed bytes.</summary>
public sealed record ArtifactStoreReadRequest
{
    /// <summary>Initializes an exact backend read request.</summary>
    /// <param name="reference">The exact portable reference.</param>
    /// <param name="grant">The single-use grant whose captured authorization carries the exact scope and identity of the operation.</param>
    /// <exception cref="ArgumentNullException">A reference value is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="grant"/> carries no captured authorization.</exception>
    public ArtifactStoreReadRequest(ArtifactReference reference, SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant);
        Reference = reference; Grant = grant;
    }

    /// <summary>Gets the exact portable reference.</summary>
    public ArtifactReference Reference { get; }

    /// <summary>Gets the single-use read grant.</summary>
    public SecurityGrant Grant { get; }

    /// <summary>Gets the exact security scope bound by the grant.</summary>
    public SecurityAuthorizationScope Scope => Grant.Scope;

    /// <summary>Gets the authenticated identity bound by the grant.</summary>
    public ExecutionIdentity Identity => Grant.Identity;
}
