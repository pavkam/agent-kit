// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

internal sealed class RejectingPolicy: IIdentityNormalizationPolicy
{
    public ValueTask<IdentityNormalizationResult> NormalizeAsync(IdentityNormalizationRequest request, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IdentityNormalizationResult>(new IdentityNormalizationRejected(
            new IdentityFailure(IdentityFailureKind.Malformed, "rejected by policy", request.Candidate.Evidence.Issuer)));
}
