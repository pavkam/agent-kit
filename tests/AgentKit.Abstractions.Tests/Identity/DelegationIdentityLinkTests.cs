// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies DelegationIdentityLink behavior and contracts.</summary>
public sealed class DelegationIdentityLinkTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var id = new DelegationId(Guid.NewGuid());
        var claim = Claim();
        var delegatedAt = DateTimeOffset.UnixEpoch;
        var link = new DelegationIdentityLink(id, new TenantId("tenant"), new PrincipalId("principal"), new IdentityIssuerId("issuer"), new AuthenticationEvidenceId("evidence"), new IdentityVersion(1), delegatedAt, [claim], IdentityAssuranceLevel.Basic);
        link.Id.ShouldBe(id);
        link.TenantId.ShouldBe(new TenantId("tenant"));
        link.PrincipalId.ShouldBe(new PrincipalId("principal"));
        link.Issuer.ShouldBe(new IdentityIssuerId("issuer"));
        link.EvidenceId.ShouldBe(new AuthenticationEvidenceId("evidence"));
        link.Version.ShouldBe(new IdentityVersion(1));
        link.DelegatedAt.ShouldBe(delegatedAt);
        link.Claims.ShouldBe([claim]);
        link.Assurance.ShouldBe(IdentityAssuranceLevel.Basic);
    }

    [Fact]
    public void Constructor_WhenIdIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Link(id: default(DelegationId)));
        exception.ParamName.ShouldBe("id");
    }

    [Fact]
    public void Constructor_WhenTenantIdIsDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => Link(tenantId: default(TenantId)));
        exception.ParamName.ShouldBe("tenantId");
    }

    [Fact]
    public void Constructor_WhenPrincipalIdIsDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => Link(principalId: default(PrincipalId)));
        exception.ParamName.ShouldBe("principalId");
    }

    [Fact]
    public void Constructor_WhenIssuerIsDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => Link(issuer: default(IdentityIssuerId)));
        exception.ParamName.ShouldBe("issuer");
    }

    [Fact]
    public void Constructor_WhenEvidenceIdIsDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => Link(evidenceId: default(AuthenticationEvidenceId)));
        exception.ParamName.ShouldBe("evidenceId");
    }

    [Fact]
    public void Constructor_WhenVersionIsNotPositive_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Link(version: default(IdentityVersion)));
        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void Constructor_WhenClaimsContainNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => Link(claims: [null!]));
        exception.ParamName.ShouldBe("claims");
    }

    [Fact]
    public void Constructor_WhenAssuranceIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Link(assurance: (IdentityAssuranceLevel) 999));
        exception.ParamName.ShouldBe("assurance");
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var id = new DelegationId(Guid.NewGuid());
        var first = Link(id: id);
        var second = Link(id: id);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Link();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static IdentityClaim Claim() => new(new IdentityIssuerId("issuer"), "role", "reader", IdentityClaimValueKind.Text);

    private static DelegationIdentityLink Link(
        DelegationId? id = null,
        TenantId? tenantId = null,
        PrincipalId? principalId = null,
        IdentityIssuerId? issuer = null,
        AuthenticationEvidenceId? evidenceId = null,
        IdentityVersion? version = null,
        ImmutableArray<IdentityClaim>? claims = null,
        IdentityAssuranceLevel assurance = IdentityAssuranceLevel.Basic) => new(
        id ?? new DelegationId(Guid.NewGuid()),
        tenantId ?? new TenantId("tenant"),
        principalId ?? new PrincipalId("principal"),
        issuer ?? new IdentityIssuerId("issuer"),
        evidenceId ?? new AuthenticationEvidenceId("evidence"),
        version ?? new IdentityVersion(1),
        DateTimeOffset.UnixEpoch,
        claims ?? [Claim()],
        assurance);
}
