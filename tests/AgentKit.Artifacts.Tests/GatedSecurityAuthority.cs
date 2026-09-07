// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

/// <summary>Pauses authorization after capturing a request so tests can mutate external configuration deterministically.</summary>
internal sealed class GatedSecurityAuthority: ISecurityAuthority
{
    private readonly TaskCompletionSource<SecurityRequest> _observed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the authorization request after the coordinator reaches the gated boundary.</summary>
    internal Task<SecurityRequest> Observed => _observed.Task;

    /// <summary>Allows the gated authorization to produce an exact grant.</summary>
    internal void Release() => _release.SetResult();

    /// <inheritdoc/>
    public async ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request, CancellationToken cancellationToken = default)
    {
        _observed.SetResult(request);
        await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        var grant = new SecurityGrant(
            new GrantId(Guid.Parse("80000000-0000-0000-0000-000000000008")), request.Id, request.Scope,
            request.Identity, request.Audience, request.Kind, request.Effect, request.Resources,
            request.InputFingerprint, new SecurityPolicyVersion(1), new SecurityRevocationVersion(1),
            ArtifactTestData.Now, request.Deadline, 1);
        return new SecurityAllowed(request.Id, new SecurityPolicyVersion(1), grant);
    }
}
