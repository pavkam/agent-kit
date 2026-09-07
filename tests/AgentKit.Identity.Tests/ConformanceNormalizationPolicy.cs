// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

internal sealed class ConformanceNormalizationPolicy(ConformanceIssuerSettings settings): IIdentityNormalizationPolicy
{
    public ValueTask<IdentityNormalizationResult> NormalizeAsync(IdentityNormalizationRequest request, CancellationToken cancellationToken = default)
    {
        var candidate = request.Candidate;
        var evidence = settings.Scenario == IdentityNormalizerScenario.ReplaceEvidence
            ? new AuthenticationEvidence(new AuthenticationEvidenceId("replacement"), candidate.Evidence.Issuer, candidate.Evidence.Method, candidate.Evidence.AuthenticatedAt, candidate.Evidence.ExpiresAt, candidate.Evidence.SafeFingerprint)
            : candidate.Evidence;
        var version = settings.Scenario == IdentityNormalizerScenario.ReplaceVersion ? new IdentityVersion(candidate.Version.Value + 1) : candidate.Version;
        var claims = settings.Scenario == IdentityNormalizerScenario.WidenClaims ? candidate.Claims.Add(new IdentityClaim(candidate.Evidence.Issuer, "scope", "write", IdentityClaimValueKind.Text)) : candidate.Claims;
        var assurance = settings.Scenario == IdentityNormalizerScenario.WidenAssurance ? IdentityAssuranceLevel.HardwareBacked : candidate.Assurance;
        var tenantId = settings.Scenario == IdentityNormalizerScenario.ReplaceTenant ? new TenantId("other") : candidate.TenantId;
        var principalId = settings.Scenario == IdentityNormalizerScenario.ReplacePrincipal ? new PrincipalId("other") : candidate.PrincipalId;
        var subjectKind = settings.Scenario == IdentityNormalizerScenario.ReplaceSubjectKind ? ExecutionSubjectKind.Service : candidate.SubjectKind;
        var delegationChain = settings.Scenario == IdentityNormalizerScenario.ReplaceDelegationChain ? [] : candidate.DelegationChain;
        return ValueTask.FromResult<IdentityNormalizationResult>(new IdentityNormalized(new ExecutionIdentity(tenantId, principalId, subjectKind, evidence, claims, delegationChain, assurance, version)));
    }
}
