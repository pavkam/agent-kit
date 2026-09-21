// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

internal sealed class GatedEvidenceIssuer(AsyncGate gate, [ServiceKey] IdentityIssuerId issuerId): IIdentityIssuer
{
    public IdentityIssuerDescriptor Descriptor { get; } = new(issuerId, new IdentityVersion(1));

    public async ValueTask<IdentityValidationResult> ValidateEvidenceAsync(
        AuthenticationEvidence evidence,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default)
    {
        _ = gate.Entered.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        return IdentityValidationPassed.Instance;
    }

    public ValueTask<IdentityNormalizationResult> NormalizeAsync(
        IdentityAssertion assertion,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IdentityNormalizationResult>(
            new IdentityNormalizationRejected(new IdentityFailure(IdentityFailureKind.Unavailable, "unused", assertion.Issuer)));
}
