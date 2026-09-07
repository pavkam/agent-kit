// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

public sealed class IdentityArgumentExceptionExtensionsTests
{
    [Fact]
    public void ThrowIfIssuerMismatch_WhenIssuerMatches_DoesNotThrow()
    {
        var evidence = Evidence("issuer");

        Should.NotThrow(() => ArgumentException.ThrowIfIssuerMismatch(evidence, new IdentityIssuerId("issuer")));
    }

    [Fact]
    public void ThrowIfIssuerMismatch_WhenIssuerDiffers_ThrowsWithInferredParameterName()
    {
        var evidence = Evidence("first");

        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfIssuerMismatch(evidence, new IdentityIssuerId("second")));

        exception.ParamName.ShouldBe("evidence");
    }

    [Fact]
    public void ThrowIfIssuerMismatch_WhenEvidenceIsNull_ThrowsArgumentNullException()
    {
        AuthenticationEvidence evidence = null!;

        var exception = Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfIssuerMismatch(evidence, new IdentityIssuerId("issuer")));

        exception.ParamName.ShouldBe("evidence");
    }

    [Fact]
    public void ThrowIfCrossesTenant_WhenAllAncestorsShareTenant_DoesNotRequireSamePrincipal()
    {
        ImmutableArray<DelegationIdentityLink> chain = [Link("tenant", "parent")];

        Should.NotThrow(() => ArgumentException.ThrowIfCrossesTenant(chain, new TenantId("tenant")));
    }

    [Fact]
    public void ThrowIfCrossesTenant_WhenAncestorUsesAnotherTenant_ThrowsWithInferredParameterName()
    {
        ImmutableArray<DelegationIdentityLink> chain = [Link("other", "parent")];

        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfCrossesTenant(chain, new TenantId("tenant")));

        exception.ParamName.ShouldBe("chain");
    }

    [Fact]
    public void ThrowIfCrossesTenant_WhenChainIsDefault_ThrowsArgumentException()
    {
        ImmutableArray<DelegationIdentityLink> chain = default;

        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfCrossesTenant(chain, new TenantId("tenant")));

        exception.ParamName.ShouldBe("chain");
    }

    [Fact]
    public void ThrowIfNotSubsetOf_WhenClaimsAreNarrower_DoesNotThrow()
    {
        var claim = Claim("reader");
        ImmutableArray<IdentityClaim> requested = [claim];
        ImmutableArray<IdentityClaim> parent = [claim, Claim("writer")];

        Should.NotThrow(() => ArgumentException.ThrowIfNotSubsetOf(requested, parent));
    }

    [Fact]
    public void ThrowIfNotSubsetOf_WhenClaimWouldBroaden_ThrowsWithInferredParameterName()
    {
        ImmutableArray<IdentityClaim> requested = [Claim("admin")];
        ImmutableArray<IdentityClaim> parent = [Claim("reader")];

        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNotSubsetOf(requested, parent));

        exception.ParamName.ShouldBe("requested");
    }

    [Fact]
    public void ThrowIfNotSubsetOf_WhenRequestedClaimsAreDefault_ThrowsArgumentException()
    {
        ImmutableArray<IdentityClaim> requested = default;
        ImmutableArray<IdentityClaim> parent = [Claim("reader")];

        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNotSubsetOf(requested, parent));

        exception.ParamName.ShouldBe("requested");
    }

    private static AuthenticationEvidence Evidence(string issuer) => new(
        new AuthenticationEvidenceId("evidence"), new IdentityIssuerId(issuer), "mfa",
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1),
        new AuthenticationEvidenceFingerprint(new ContentHash("safe-hash")));

    private static DelegationIdentityLink Link(string tenant, string principal) => new(
        new DelegationId(Guid.NewGuid()), new TenantId(tenant), new PrincipalId(principal),
        new IdentityIssuerId("issuer"), new AuthenticationEvidenceId("evidence"), new IdentityVersion(1),
        DateTimeOffset.UnixEpoch, [Claim("reader")], IdentityAssuranceLevel.Strong);

    private static IdentityClaim Claim(string value) => new(
        new IdentityIssuerId("issuer"), "role", value, IdentityClaimValueKind.Text);
}
