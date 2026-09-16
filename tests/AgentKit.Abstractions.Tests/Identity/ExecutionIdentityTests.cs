// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies ExecutionIdentity behavior and contracts.</summary>
public sealed class ExecutionIdentityTests
{
    [Fact]
    public void ExecutionIdentity_WhenDelegationChangesTenant_ThrowsArgumentException()
    {
        var claim = Claim();
        var link = Link("other", "principal", claim, IdentityAssuranceLevel.Basic);
        var exception = Should.Throw<ArgumentException>(() => new ExecutionIdentity(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human, Evidence("issuer"), [claim], [link], IdentityAssuranceLevel.Basic, new IdentityVersion(1)));
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
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ExecutionIdentity(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human, Evidence("issuer"), [], [], IdentityAssuranceLevel.Basic, default));
        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void Equals_WhenDelegationChainsMatch_HasEqualHashCode()
    {
        var claim = Claim();
        var link = Link("tenant", "requester", claim, IdentityAssuranceLevel.Strong);
        var first = new ExecutionIdentity(new TenantId("tenant"), new PrincipalId("impersonated"), ExecutionSubjectKind.Human, Evidence("issuer"), [claim], [link], IdentityAssuranceLevel.Basic, new IdentityVersion(1));
        var second = new ExecutionIdentity(new TenantId("tenant"), new PrincipalId("impersonated"), ExecutionSubjectKind.Human, Evidence("issuer"), [claim], [link], IdentityAssuranceLevel.Basic, new IdentityVersion(1));
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var claim = Claim();
        var original = new ExecutionIdentity(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human, Evidence("issuer"), [claim], [], IdentityAssuranceLevel.Basic, new IdentityVersion(1));
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static IdentityClaim Claim() => new(new IdentityIssuerId("issuer"), "role", "reader", IdentityClaimValueKind.Text);
    private static DelegationIdentityLink Link(string tenant, string principal, IdentityClaim claim, IdentityAssuranceLevel assurance) => new(new DelegationId(Guid.NewGuid()), new TenantId(tenant), new PrincipalId(principal), new IdentityIssuerId("issuer"), new AuthenticationEvidenceId("evidence"), new IdentityVersion(1), DateTimeOffset.UnixEpoch, [claim], assurance);
    private static AuthenticationEvidence Evidence(string issuer) => new(new AuthenticationEvidenceId("evidence"), new IdentityIssuerId(issuer), "mfa", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1), new AuthenticationEvidenceFingerprint(new ContentHash("safe-hash")));
}
