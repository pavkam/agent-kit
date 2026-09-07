// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Derives same-tenant, same-principal child identities that can only narrow parent identity evidence.</summary>
public interface IDelegatedIdentityDeriver
{
    /// <summary>Derives a child identity while retaining provenance and rejecting any broadening.</summary>
    /// <param name="request">The bounded delegation request.</param>
    /// <param name="cancellationToken">Signals that the caller no longer needs derivation.</param>
    /// <returns>A narrowed child identity or typed rejection.</returns>
    public ValueTask<IdentityResolutionResult> DeriveAsync(DelegatedIdentityRequest request, CancellationToken cancellationToken = default);
}
