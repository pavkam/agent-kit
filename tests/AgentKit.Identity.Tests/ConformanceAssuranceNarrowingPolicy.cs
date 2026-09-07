// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

internal sealed class ConformanceAssuranceNarrowingPolicy: IIdentityNormalizationPolicy
{
    public ValueTask<IdentityNormalizationResult> NormalizeAsync(IdentityNormalizationRequest request, CancellationToken cancellationToken = default)
    {
        var candidate = request.Candidate;
        var narrowed = new ExecutionIdentity(candidate.TenantId, candidate.PrincipalId, candidate.SubjectKind, candidate.Evidence, candidate.Claims, candidate.DelegationChain, IdentityAssuranceLevel.Basic, candidate.Version);
        return ValueTask.FromResult<IdentityNormalizationResult>(new IdentityNormalized(narrowed));
    }
}
