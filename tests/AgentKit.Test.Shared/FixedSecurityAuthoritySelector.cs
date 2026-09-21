// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Returns one configured authority for every captured authorization context in tests.</summary>
public sealed class FixedSecurityAuthoritySelector: ISecurityAuthoritySelector
{
    private readonly ISecurityAuthority _authority;

    /// <summary>Initializes a selector that always activates <paramref name="authority"/>.</summary>
    /// <param name="authority">The authority returned for every selection.</param>
    /// <exception cref="ArgumentNullException"><paramref name="authority"/> is null.</exception>
    public FixedSecurityAuthoritySelector(ISecurityAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
        _authority = authority;
    }

    /// <inheritdoc/>
    public ValueTask<SecurityAuthoritySelectionResult> SelectAsync(
        SecurityAuthorizationContext authorization,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SecurityAuthoritySelectionResult>(
            new SecurityAuthoritySelected(authorization, _authority));
    }
}
