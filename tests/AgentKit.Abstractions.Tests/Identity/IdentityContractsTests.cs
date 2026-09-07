// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

public sealed class IdentityContractsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void IdentityIssuerId_WhenValueIsBlank_ThrowsArgumentException(string? value)
    {
        var exception = Should.Throw<ArgumentException>(() => new IdentityIssuerId(value!));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void IdentityVersion_WhenValueIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new IdentityVersion(-1));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void IdentityVersion_WhenValueIsZero_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new IdentityVersion(0));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void AuthenticationEvidence_WhenExpiryDoesNotFollowAuthentication_ThrowsBeforeConstruction()
    {
        var authenticatedAt = DateTimeOffset.UnixEpoch;

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new AuthenticationEvidence(
            new AuthenticationEvidenceId("evidence"), new IdentityIssuerId("issuer"), "mfa",
            authenticatedAt, authenticatedAt, new AuthenticationEvidenceFingerprint(new ContentHash("hash"))));

        exception.ParamName.ShouldBe("expiresAt");
    }

    [Fact]
    public void IdentityAssertion_WhenEvidenceIssuerDiffers_ThrowsArgumentException()
    {
        var evidence = Evidence("issuer-a");

        var exception = Should.Throw<ArgumentException>(() => new IdentityAssertion(
            new IdentityIssuerId("issuer-b"), "external-subject", [], evidence));

        exception.ParamName.ShouldBe("evidence");
    }

    [Fact]
    public void IdentityAssertion_WhenClaimsUseDifferentArrayInstances_HasStructuralEquality()
    {
        var evidence = Evidence("issuer");
        var first = new IdentityAssertion(new IdentityIssuerId("issuer"), "subject", [new ExternalIdentityClaim("group", "a", IdentityClaimValueKind.Text)], evidence);
        var second = new IdentityAssertion(new IdentityIssuerId("issuer"), "subject", [new ExternalIdentityClaim("group", "a", IdentityClaimValueKind.Text)], evidence);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void ExecutionIdentity_WhenDelegationChangesTenant_ThrowsArgumentException()
    {
        var claim = Claim();
        var link = Link("other", "principal", claim, IdentityAssuranceLevel.Basic);

        var exception = Should.Throw<ArgumentException>(() => new ExecutionIdentity(
            new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human,
            Evidence("issuer"), [claim], [link], IdentityAssuranceLevel.Basic, new IdentityVersion(1)));

        exception.ParamName.ShouldBe("delegationChain");
    }

    [Fact]
    public void ExecutionIdentity_WhenArraysHaveEqualContents_HasStructuralEquality()
    {
        var claim = Claim();
        var evidence = Evidence("issuer");
        var first = new ExecutionIdentity(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human, evidence, [claim], [], IdentityAssuranceLevel.Strong, new IdentityVersion(3));
        var second = new ExecutionIdentity(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human, evidence, [claim with { }], [], IdentityAssuranceLevel.Strong, new IdentityVersion(3));

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void ExecutionIdentity_WhenAncestorPrincipalDiffersWithinTenant_PreservesImpersonationProvenance()
    {
        var claim = Claim();
        var link = Link("tenant", "requester", claim, IdentityAssuranceLevel.Strong);

        var identity = new ExecutionIdentity(new TenantId("tenant"), new PrincipalId("impersonated"), ExecutionSubjectKind.Human, Evidence("issuer"), [claim], [link], IdentityAssuranceLevel.Basic, new IdentityVersion(1));

        identity.DelegationChain[0].PrincipalId.ShouldBe(new PrincipalId("requester"));
    }

    [Fact]
    public void ExecutionIdentity_WhenChildAssuranceExceedsAncestor_ThrowsArgumentOutOfRangeException()
    {
        var claim = Claim();
        var link = Link("tenant", "principal", claim, IdentityAssuranceLevel.Basic);

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ExecutionIdentity(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human, Evidence("issuer"), [claim], [link], IdentityAssuranceLevel.Strong, new IdentityVersion(1)));

        exception.ParamName.ShouldBe("assurance");
    }

    [Fact]
    public void ExecutionIdentity_WhenVersionIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ExecutionIdentity(
            new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human,
            Evidence("issuer"), [], [], IdentityAssuranceLevel.Basic, default));

        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void DelegatedIdentityRequest_WhenClaimIsNotInParent_ThrowsArgumentException()
    {
        var parent = Identity([Claim()]);
        var untrustedBroadening = new IdentityClaim(new IdentityIssuerId("issuer"), "role", "admin", IdentityClaimValueKind.Text);

        var exception = Should.Throw<ArgumentException>(() => new DelegatedIdentityRequest(
            new DelegationId(Guid.NewGuid()), parent, [untrustedBroadening], IdentityAssuranceLevel.Basic));

        exception.ParamName.ShouldBe("claims");
    }

    [Fact]
    public void DelegatedIdentityRequest_WhenAssuranceExceedsParent_ThrowsArgumentOutOfRangeException()
    {
        var parent = Identity([Claim()], IdentityAssuranceLevel.Basic);

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DelegatedIdentityRequest(
            new DelegationId(Guid.NewGuid()), parent, [], IdentityAssuranceLevel.Strong));

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

    private static DelegationIdentityLink Link(string tenant, string principal, IdentityClaim claim, IdentityAssuranceLevel assurance) => new(
        new DelegationId(Guid.NewGuid()), new TenantId(tenant), new PrincipalId(principal),
        new IdentityIssuerId("issuer"), new AuthenticationEvidenceId("evidence"), new IdentityVersion(1),
        DateTimeOffset.UnixEpoch, [claim], assurance);

    private static AuthenticationEvidence Evidence(string issuer) => new(
        new AuthenticationEvidenceId("evidence"), new IdentityIssuerId(issuer), "mfa",
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1),
        new AuthenticationEvidenceFingerprint(new ContentHash("safe-hash")));

    private static ExecutionIdentity Identity(ImmutableArray<IdentityClaim> claims, IdentityAssuranceLevel assurance = IdentityAssuranceLevel.Basic) => new(
        new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human,
        Evidence("issuer"), claims, [], assurance, new IdentityVersion(1));
}
