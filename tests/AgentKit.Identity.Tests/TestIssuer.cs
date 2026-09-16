// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

internal sealed class TestIssuer(TestIssuerSettings settings, [ServiceKey] IdentityIssuerId issuerId): IIdentityIssuer
{
    public IdentityIssuerDescriptor Descriptor { get; } = new(issuerId, new IdentityVersion(settings.Version));
    public ValueTask<IdentityValidationResult> ValidateEvidenceAsync(AuthenticationEvidence evidence, DateTimeOffset evaluatedAt, CancellationToken cancellationToken = default) => ValueTask.FromResult<IdentityValidationResult>(settings.Revoked ? new IdentityValidationRejected(new IdentityFailure(IdentityFailureKind.Revoked, "revoked", Descriptor.Id)) : IdentityValidationPassed.Instance);
    public ValueTask<IdentityNormalizationResult> NormalizeAsync(IdentityAssertion assertion, CancellationToken cancellationToken = default) => ValueTask.FromResult<IdentityNormalizationResult>(settings.NullNormalization
        ? null!
        : settings.RejectNormalization
            ? new IdentityNormalizationRejected(new IdentityFailure(IdentityFailureKind.Malformed, "rejected by issuer", assertion.Issuer))
            : new IdentityNormalized(new ExecutionIdentity(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human, Evidence(assertion.Issuer.Value, settings.AuthenticatedAt, settings.ExpiresAt), [Claim(assertion.Issuer.Value, "scope", "read")], [], IdentityAssuranceLevel.Basic, new IdentityVersion(1))));
}
