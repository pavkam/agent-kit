// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Activates the authority captured by one immutable authorization context.</summary>
/// <remarks>
/// Selection resolves only the exact authority key retained in the supplied authorization context.
/// It neither evaluates a request nor issues, consumes, or broadens a grant. Implementations
/// return a typed unsuccessful result when the retained authority binding cannot be activated.
/// </remarks>
public interface ISecurityAuthoritySelector
{
    /// <summary>Resolves the exact authority binding captured for one operation.</summary>
    /// <param name="authorization">The immutable capture whose authority key must be resolved exactly.</param>
    /// <param name="cancellationToken">Cancels selection before a successful activation is returned.</param>
    /// <returns>The selected authority or a typed unavailable result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="authorization"/> is null.</exception>
    public ValueTask<SecurityAuthoritySelectionResult> SelectAsync(
        SecurityAuthorizationContext authorization,
        CancellationToken cancellationToken = default);
}
