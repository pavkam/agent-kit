// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries exact read authority to the backend owning committed bytes.</summary>
public sealed record ArtifactStoreReadRequest
{
    /// <summary>Initializes an exact backend read request.</summary>
    /// <param name="reference">The exact portable reference.</param>
    /// <param name="scope">The exact authorization scope.</param>
    /// <param name="identity">The authenticated identity.</param>
    /// <param name="grant">The single-use read grant.</param>
    /// <exception cref="ArgumentNullException">A reference value is null.</exception>
    public ArtifactStoreReadRequest(ArtifactReference reference, SecurityAuthorizationScope scope, ExecutionIdentity identity, SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(grant);
        Reference = reference;
        Scope = scope;
        Identity = identity;
        Grant = grant;
    }

    /// <summary>Gets the exact portable reference.</summary>
    public ArtifactReference Reference { get; }
    /// <summary>Gets the exact authorization scope.</summary>
    public SecurityAuthorizationScope Scope { get; }
    /// <summary>Gets the authenticated identity.</summary>
    public ExecutionIdentity Identity { get; }
    /// <summary>Gets the single-use read grant.</summary>
    public SecurityGrant Grant { get; }
}
