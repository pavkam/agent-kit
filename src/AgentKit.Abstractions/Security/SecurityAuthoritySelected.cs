// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports activation of the authority named by one captured authorization context.</summary>
/// <remarks>
/// <see cref="Authorization"/> remains the exact captured evidence used for selection. The
/// selected authority is not a grant and cannot authorize a request without evaluating it.
/// </remarks>
public sealed record SecurityAuthoritySelected: SecurityAuthoritySelectionResult
{
    /// <summary>Initializes one successful authority activation.</summary>
    /// <param name="authorization">The exact captured evidence that selected <paramref name="authority"/>.</param>
    /// <param name="authority">The non-null authority bound to the captured key.</param>
    /// <exception cref="ArgumentNullException">A supplied reference is null.</exception>
    public SecurityAuthoritySelected(SecurityAuthorizationContext authorization, ISecurityAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(authority);
        Authorization = authorization;
        Authority = authority;
    }

    /// <summary>Gets the immutable captured evidence that identified the selected authority.</summary>
    /// <value>The exact context passed to the selector without a substituted scope or profile.</value>
    public SecurityAuthorizationContext Authorization { get; }

    /// <summary>Gets the activated authority for the caller's bounded operation lifetime.</summary>
    /// <value>A non-null authority that must independently authorize each normalized request.</value>
    public ISecurityAuthority Authority { get; }
}
