// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

/// <summary>Verifies the <see cref="ExecutionIdentityExtensions"/> factories, their argument checks, and fingerprint safety.</summary>
public sealed class ExecutionIdentityExtensionsTests
{
    private static readonly TenantId _tenant = new("acme");
    private static readonly PrincipalId _principal = new("triage-worker");
    private static readonly IdentityIssuerId _issuer = new("azure-managed-identity");
    private static readonly DateTimeOffset _authenticatedAt = new(2026, 9, 18, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ForService_WhenArgumentsAreValid_BuildsAStrongServiceIdentityWithDerivedEvidence()
    {
        var identity = ExecutionIdentity.ForService(_tenant, _principal, _issuer, "managed-identity", _authenticatedAt);

        identity.TenantId.ShouldBe(_tenant);
        identity.PrincipalId.ShouldBe(_principal);
        identity.SubjectKind.ShouldBe(ExecutionSubjectKind.Service);
        identity.Assurance.ShouldBe(IdentityAssuranceLevel.Strong);
        identity.Version.ShouldBe(new IdentityVersion(1));
        identity.Claims.ShouldBeEmpty();
        identity.DelegationChain.ShouldBeEmpty();
        identity.Evidence.Issuer.ShouldBe(_issuer);
        identity.Evidence.Method.ShouldBe("managed-identity");
        identity.Evidence.AuthenticatedAt.ShouldBe(_authenticatedAt);
        identity.Evidence.ExpiresAt.ShouldBeNull();
        identity.Evidence.Id.Value.ShouldContain(_principal.Value);
        identity.Evidence.SafeFingerprint.Hash.Value.ShouldStartWith("sha256:");
    }

    [Fact]
    public void ForHuman_WhenArgumentsAreValid_BuildsABasicHumanIdentityHonoringExpiryAndClaims()
    {
        var claims = ImmutableArray.Create(new IdentityClaim(_issuer, "role", "support-agent", IdentityClaimValueKind.Text));
        var expiresAt = _authenticatedAt.AddHours(8);

        var identity = ExecutionIdentity.ForHuman(_tenant, new PrincipalId("sub-123"), _issuer, "oidc", _authenticatedAt, expiresAt, claims: claims);

        identity.SubjectKind.ShouldBe(ExecutionSubjectKind.Human);
        identity.Assurance.ShouldBe(IdentityAssuranceLevel.Basic);
        identity.Evidence.ExpiresAt.ShouldBe(expiresAt);
        identity.Claims.ShouldBe(claims);
    }

    [Fact]
    public void ForHuman_WhenAssuranceIsSupplied_UsesIt()
    {
        var identity = ExecutionIdentity.ForHuman(_tenant, _principal, _issuer, "webauthn", _authenticatedAt, assurance: IdentityAssuranceLevel.HardwareBacked);

        identity.Assurance.ShouldBe(IdentityAssuranceLevel.HardwareBacked);
    }

    [Fact]
    public void ForService_WhenCalledTwiceWithTheSameFacts_ProducesEqualIdentities()
    {
        var first = ExecutionIdentity.ForService(_tenant, _principal, _issuer, "managed-identity", _authenticatedAt);
        var second = ExecutionIdentity.ForService(_tenant, _principal, _issuer, "managed-identity", _authenticatedAt);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Fingerprint_WhenAnyInputDiffers_Differs()
    {
        var baseline = ExecutionIdentityExtensions.Fingerprint(_issuer, _principal, "oidc", _authenticatedAt);

        ExecutionIdentityExtensions.Fingerprint(new IdentityIssuerId("other"), _principal, "oidc", _authenticatedAt).ShouldNotBe(baseline);
        ExecutionIdentityExtensions.Fingerprint(_issuer, new PrincipalId("other"), "oidc", _authenticatedAt).ShouldNotBe(baseline);
        ExecutionIdentityExtensions.Fingerprint(_issuer, _principal, "saml", _authenticatedAt).ShouldNotBe(baseline);
        ExecutionIdentityExtensions.Fingerprint(_issuer, _principal, "oidc", _authenticatedAt.AddSeconds(1)).ShouldNotBe(baseline);
        ExecutionIdentityExtensions.Fingerprint(_issuer, _principal, "oidc", _authenticatedAt).ShouldBe(baseline);
    }

    [Fact]
    public void Fingerprint_WhenComputed_DoesNotEmbedTheInputsInClearText()
    {
        var fingerprint = ExecutionIdentityExtensions.Fingerprint(_issuer, _principal, "oidc", _authenticatedAt);

        fingerprint.Hash.Value.ShouldNotContain(_principal.Value);
        fingerprint.Hash.Value.ShouldNotContain(_issuer.Value);
        fingerprint.Hash.Value.Length.ShouldBe("sha256:".Length + 64);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Fingerprint_WhenMethodIsBlank_ThrowsArgumentException(string? method) =>
        Should.Throw<ArgumentException>(() => ExecutionIdentityExtensions.Fingerprint(_issuer, _principal, method!, _authenticatedAt))
            .ParamName.ShouldBe("method");

    [Fact]
    public void Fingerprint_WhenIssuerIsDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => ExecutionIdentityExtensions.Fingerprint(default, _principal, "oidc", _authenticatedAt))
            .ParamName.ShouldBe("issuer");

    [Fact]
    public void Fingerprint_WhenSubjectIsDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => ExecutionIdentityExtensions.Fingerprint(_issuer, default, "oidc", _authenticatedAt))
            .ParamName.ShouldBe("subject");

    [Fact]
    public void ForService_WhenTenantIsDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => ExecutionIdentity.ForService(default, _principal, _issuer, "m", _authenticatedAt))
            .ParamName.ShouldBe("tenantId");

    [Fact]
    public void ForService_WhenPrincipalIsDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => ExecutionIdentity.ForService(_tenant, default, _issuer, "m", _authenticatedAt))
            .ParamName.ShouldBe("principalId");

    [Fact]
    public void ForService_WhenIssuerIsDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => ExecutionIdentity.ForService(_tenant, _principal, default, "m", _authenticatedAt))
            .ParamName.ShouldBe("issuer");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ForHuman_WhenMethodIsBlank_ThrowsArgumentException(string? method) =>
        Should.Throw<ArgumentException>(() => ExecutionIdentity.ForHuman(_tenant, _principal, _issuer, method!, _authenticatedAt))
            .ParamName.ShouldBe("method");

    [Fact]
    public void ForService_WhenExpiryIsNotAfterAuthentication_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => ExecutionIdentity.ForService(_tenant, _principal, _issuer, "m", _authenticatedAt, _authenticatedAt))
            .ParamName.ShouldBe("expiresAt");

    [Fact]
    public void ForService_WhenAssuranceIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => ExecutionIdentity.ForService(_tenant, _principal, _issuer, "m", _authenticatedAt, assurance: (IdentityAssuranceLevel) 99))
            .ParamName.ShouldBe("assurance");

    [Fact]
    public void ForService_WhenClaimsContainNull_ThrowsArgumentException()
    {
        ImmutableArray<IdentityClaim> claims = [null!];

        Should.Throw<ArgumentException>(() => ExecutionIdentity.ForService(_tenant, _principal, _issuer, "m", _authenticatedAt, claims: claims))
            .ParamName.ShouldBe("claims");
    }
}
