// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies DelegatedIdentityRequest behavior and contracts.</summary>
public sealed class DelegatedIdentityRequestTests
{
    [Fact]
    public void DelegatedIdentityRequest_WhenClaimIsNotInParent_ThrowsArgumentException()
    {
        var parent = Identity([Claim()]);
        var untrustedBroadening = new IdentityClaim(new IdentityIssuerId("issuer"), "role", "admin", IdentityClaimValueKind.Text);
        var exception = Should.Throw<ArgumentException>(() => new DelegatedIdentityRequest(new DelegationId(Guid.NewGuid()), parent, [untrustedBroadening], IdentityAssuranceLevel.Basic));
        exception.ParamName.ShouldBe("claims");
    }

    [Fact]
    public void DelegatedIdentityRequest_WhenAssuranceExceedsParent_ThrowsArgumentOutOfRangeException()
    {
        var parent = Identity([Claim()], IdentityAssuranceLevel.Basic);
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DelegatedIdentityRequest(new DelegationId(Guid.NewGuid()), parent, [], IdentityAssuranceLevel.Strong));
        exception.ParamName.ShouldBe("maximumAssurance");
    }

    [Fact]
    public void DelegatedIdentityRequest_PublicShape_HasNoTenantOrPrincipalOverride()
    {
        var properties = typeof(DelegatedIdentityRequest).GetProperties().Select(property => property.Name).ToArray();
        properties.ShouldNotContain("TenantId");
        properties.ShouldNotContain("PrincipalId");
    }

    private static IdentityClaim Claim() => new(new IdentityIssuerId("issuer"), "role", "reader", IdentityClaimValueKind.Text);
    private static AuthenticationEvidence Evidence(string issuer) => new(new AuthenticationEvidenceId("evidence"), new IdentityIssuerId(issuer), "mfa", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1), new AuthenticationEvidenceFingerprint(new ContentHash("safe-hash")));
    private static ExecutionIdentity Identity(ImmutableArray<IdentityClaim> claims, IdentityAssuranceLevel assurance = IdentityAssuranceLevel.Basic) => new(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human, Evidence("issuer"), claims, [], assurance, new IdentityVersion(1));
}
