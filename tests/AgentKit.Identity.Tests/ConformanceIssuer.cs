// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

internal sealed class ConformanceIssuer(ConformanceIssuerSettings settings, [ServiceKey] IdentityIssuerId issuerId): IIdentityIssuer
{
    public IdentityIssuerDescriptor Descriptor => new(issuerId, new IdentityVersion(7));

    public ValueTask<IdentityValidationResult> ValidateEvidenceAsync(AuthenticationEvidence evidence, DateTimeOffset evaluatedAt, CancellationToken cancellationToken = default) => ValueTask.FromResult<IdentityValidationResult>(IdentityValidationPassed.Instance);

    public async ValueTask<IdentityNormalizationResult> NormalizeAsync(IdentityAssertion assertion, CancellationToken cancellationToken = default)
    {
        if (settings.Scenario == IdentityNormalizerScenario.IssuerUnavailable)
        {
            return null!;
        }

        if (settings.Scenario == IdentityNormalizerScenario.BlockingIssuer)
        {
            _ = settings.Entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        var evidence = settings.Scenario == IdentityNormalizerScenario.MalformedIssuer
            ? new AuthenticationEvidence(new AuthenticationEvidenceId("wrong"), new IdentityIssuerId("other"), "test", assertion.Evidence.AuthenticatedAt, assertion.Evidence.ExpiresAt, assertion.Evidence.SafeFingerprint)
            : assertion.Evidence;
        var claims = ImmutableArray.Create(new IdentityClaim(assertion.Issuer, "scope", "read", IdentityClaimValueKind.Text), new IdentityClaim(assertion.Issuer, "role", "writer", IdentityClaimValueKind.Text));
        var delegation = new DelegationIdentityLink(new DelegationId(new Guid("11111111-1111-1111-1111-111111111111")), new TenantId("tenant"), new PrincipalId("parent"), assertion.Issuer, evidence.Id, new IdentityVersion(1), assertion.Evidence.AuthenticatedAt, claims, IdentityAssuranceLevel.HardwareBacked);
        return new IdentityNormalized(new ExecutionIdentity(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human, evidence, claims, [delegation], IdentityAssuranceLevel.Strong, new IdentityVersion(1)));
    }
}
