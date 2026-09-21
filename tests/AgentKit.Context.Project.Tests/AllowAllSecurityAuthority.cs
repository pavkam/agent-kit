// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Project.Tests;

/// <summary>Test-only security authority that allows every normalized request.</summary>
internal sealed class AllowAllSecurityAuthority(ISecurityGrantStore grantStore): ISecurityAuthority
{
    /// <inheritdoc/>
    public async ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var grant = new SecurityGrant(
            new GrantId(Guid.NewGuid()),
            request.Id,
            request.Scope,
            request.Identity,
            request.Audience,
            request.Kind,
            request.Effect,
            request.Resources,
            request.InputFingerprint,
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            DateTimeOffset.UnixEpoch,
            request.Deadline,
            request.RequestedUses);
        await grantStore.RegisterAsync(grant, cancellationToken).ConfigureAwait(false);
        return new SecurityAllowed(request.Id, new SecurityPolicyVersion(1), grant);
    }
}
