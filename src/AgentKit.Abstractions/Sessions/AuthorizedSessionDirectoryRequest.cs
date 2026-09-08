// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Pairs one exact session-directory request with the grant the directory must consume.</summary>
/// <typeparam name="TRequest">The immutable request shape enforced by the directory.</typeparam>
/// <remarks>A wrapper is bound to one request and one effecting directory call. It cannot grant access to a store, a different request, or a retry.</remarks>
public sealed record AuthorizedSessionDirectoryRequest<TRequest>
    where TRequest : class
{
    /// <summary>Initializes an authorized directory request.</summary>
    /// <param name="request">The non-null immutable directory request.</param>
    /// <param name="grant">The non-null, single-use grant issued for this exact directory access.</param>
    /// <param name="intent">The non-null stable intent whose authoritative receipt records grant consumption.</param>
    /// <exception cref="ArgumentNullException">A supplied reference is null.</exception>
    public AuthorizedSessionDirectoryRequest(TRequest request, SecurityGrant grant, SecurityEnforcementIntent intent)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(grant); ArgumentNullException.ThrowIfNull(intent);
        Request = request; Grant = grant; Intent = intent;
    }

    /// <summary>Gets the immutable directory request.</summary><value>The exact request whose enforcement evidence is recomputed.</value>
    public TRequest Request { get; }
    /// <summary>Gets the presented grant.</summary><value>Immutable authority evidence consumed by the directory's grant store.</value>
    public SecurityGrant Grant { get; }
    /// <summary>Gets the stable enforcement intent.</summary><value>The non-null identity that the grant store receipt must bind exactly.</value>
    public SecurityEnforcementIntent Intent { get; }
}
