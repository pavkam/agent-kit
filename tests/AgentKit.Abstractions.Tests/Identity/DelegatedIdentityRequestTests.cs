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
    public void Constructor_WhenDelegationIdIsEmpty_ThrowsExactParameter()
    {
        var parent = Identity([Claim()]);
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DelegatedIdentityRequest(default, parent, [], IdentityAssuranceLevel.Basic));
        exception.ParamName.ShouldBe("delegationId");
    }

    [Fact]
    public void Constructor_WhenParentIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DelegatedIdentityRequest(new DelegationId(Guid.NewGuid()), null!, [], IdentityAssuranceLevel.Basic));
        exception.ParamName.ShouldBe("parent");
    }

    [Fact]
    public void Constructor_WhenClaimsContainNull_ThrowsExactParameter()
    {
        var parent = Identity([Claim()]);
        var exception = Should.Throw<ArgumentException>(() => new DelegatedIdentityRequest(new DelegationId(Guid.NewGuid()), parent, [null!], IdentityAssuranceLevel.Basic));
        exception.ParamName.ShouldBe("claims");
    }

    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var claim = Claim();
        var parent = Identity([claim]);
        var delegationId = new DelegationId(Guid.NewGuid());
        var request = new DelegatedIdentityRequest(delegationId, parent, [claim], IdentityAssuranceLevel.Basic);
        request.DelegationId.ShouldBe(delegationId);
        request.Parent.ShouldBe(parent);
        request.Claims.ShouldBe([claim]);
        request.MaximumAssurance.ShouldBe(IdentityAssuranceLevel.Basic);
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var claim = Claim();
        var parent = Identity([claim]);
        var delegationId = new DelegationId(Guid.NewGuid());
        var first = new DelegatedIdentityRequest(delegationId, parent, [claim], IdentityAssuranceLevel.Basic);
        var second = new DelegatedIdentityRequest(delegationId, parent, [claim], IdentityAssuranceLevel.Basic);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var claim = Claim();
        var parent = Identity([claim]);
        var original = new DelegatedIdentityRequest(new DelegationId(Guid.NewGuid()), parent, [claim], IdentityAssuranceLevel.Basic);
        var copy = original with { };
        copy.ShouldBe(original);
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
