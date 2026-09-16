// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;



/// <summary>Verifies DefaultIdentityValidationPolicy behavior and contracts.</summary>
public sealed class DefaultIdentityValidationPolicyTests
{
    [Theory]
    [InlineData(0, "issuers")]
    [InlineData(1, "timeProvider")]
    [InlineData(2, "options")]
    public void DefaultIdentityValidationPolicy_WhenDependencyIsNull_ThrowsWithParameterName(int dependency, string parameterName)
    {
        var issuers = new IdentityIssuerCatalog([]);
        var clock = new FakeTimeProvider();
        var options = new AgentIdentityOptionsSnapshot(false, 1, TimeSpan.Zero, TimeSpan.FromHours(1));
        var exception = Should.Throw<ArgumentNullException>(() => _ = dependency switch
        {
            0 => new DefaultIdentityValidationPolicy(null!, clock, options),
            1 => new DefaultIdentityValidationPolicy(issuers, null!, options),
            _ => new DefaultIdentityValidationPolicy(issuers, clock, null!),
        });
        exception.ParamName.ShouldBe(parameterName);
    }

    [Fact]
    public async Task ValidateAsync_WhenSubjectIsAnonymousAndAnonymousIsDisallowed_RejectsAsUnsupported()
    {
        var now = DateTimeOffset.UnixEpoch;
        var clock = new FakeTimeProvider(now);
        var options = new AgentIdentityOptionsSnapshot(false, 1, TimeSpan.FromMinutes(1), TimeSpan.FromHours(1));
        var policy = new DefaultIdentityValidationPolicy(new IdentityIssuerCatalog([]), clock, options);
        var identity = new ExecutionIdentity(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Anonymous,
            IdentityTestData.Evidence("issuer", now, null),
            [],
            [],
            IdentityAssuranceLevel.Basic,
            new IdentityVersion(1));

        var result = await policy.ValidateAsync(identity, TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<IdentityValidationRejected>();
        rejected.Failure.Kind.ShouldBe(IdentityFailureKind.Unsupported);
    }

    [Fact]
    public async Task ValidateAsync_WhenAuthenticatedAtIsInTheFutureBeyondClockSkew_RejectsAsMalformed()
    {
        var now = DateTimeOffset.UnixEpoch;
        var clock = new FakeTimeProvider(now);
        var options = new AgentIdentityOptionsSnapshot(false, 1, TimeSpan.FromMinutes(1), TimeSpan.FromHours(1));
        var policy = new DefaultIdentityValidationPolicy(new IdentityIssuerCatalog([]), clock, options);
        var identity = Identity(IdentityTestData.Evidence("issuer", now.AddMinutes(5), null));

        var result = await policy.ValidateAsync(identity, TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<IdentityValidationRejected>();
        rejected.Failure.Kind.ShouldBe(IdentityFailureKind.Malformed);
    }

    [Fact]
    public async Task ValidateAsync_WhenEvidenceExpiredBeyondClockSkew_RejectsAsExpired()
    {
        var now = DateTimeOffset.UnixEpoch.AddHours(1);
        var clock = new FakeTimeProvider(now);
        var options = new AgentIdentityOptionsSnapshot(false, 1, TimeSpan.FromMinutes(1), TimeSpan.FromHours(2));
        var policy = new DefaultIdentityValidationPolicy(new IdentityIssuerCatalog([]), clock, options);
        var identity = Identity(IdentityTestData.Evidence(
            "issuer", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(5)));

        var result = await policy.ValidateAsync(identity, TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<IdentityValidationRejected>();
        rejected.Failure.Kind.ShouldBe(IdentityFailureKind.Expired);
        rejected.Failure.SafeMessage.ShouldBe("Authentication evidence has expired.");
    }

    private static ExecutionIdentity Identity(AuthenticationEvidence evidence) => new(
        new TenantId("tenant"),
        new PrincipalId("principal"),
        ExecutionSubjectKind.Human,
        evidence,
        [],
        [],
        IdentityAssuranceLevel.Basic,
        new IdentityVersion(1));
}
