// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

internal sealed class MutableDescriptorIssuer([ServiceKey] IdentityIssuerId issuerId): IIdentityIssuer
{
    private IdentityVersion _version = new(7);

    public IdentityIssuerDescriptor Descriptor => new(issuerId, _version);

    public ValueTask<IdentityValidationResult> ValidateEvidenceAsync(AuthenticationEvidence evidence, DateTimeOffset evaluatedAt, CancellationToken cancellationToken = default) => ValueTask.FromResult<IdentityValidationResult>(IdentityValidationPassed.Instance);

    public ValueTask<IdentityNormalizationResult> NormalizeAsync(IdentityAssertion assertion, CancellationToken cancellationToken = default)
    {
        _version = new IdentityVersion(9);
        return ValueTask.FromResult<IdentityNormalizationResult>(new IdentityNormalized(new ExecutionIdentity(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human, assertion.Evidence, [], [], IdentityAssuranceLevel.Basic, _version)));
    }
}
