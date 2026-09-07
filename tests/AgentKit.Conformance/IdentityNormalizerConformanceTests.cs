// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Defines reusable trusted-ingress identity-normalization contract cases.</summary>
/// <typeparam name="TFixture">The implementation fixture composed for each case.</typeparam>
public abstract class IdentityNormalizerConformanceTests<TFixture>
    where TFixture : IIdentityNormalizerConformanceFixture
{
    /// <summary>Creates an isolated deterministic fixture.</summary>
    /// <returns>The fixture for one contract case.</returns>
    protected abstract TFixture CreateFixture();

    /// <summary>Verifies issuer evidence and descriptor version are captured exactly.</summary>
    [Fact]
    public async Task ResolveAsync_WhenIssuerEvidenceIsValid_BindsExactEvidenceAndDescriptorVersion()
    {
        var fixture = CreateFixture();
        var assertion = Assertion(fixture.Now.AddMinutes(1));
        var result = await fixture.ResolveAsync(assertion, IdentityNormalizerScenario.Valid, TestContext.Current.CancellationToken);
        var identity = result.ShouldBeOfType<IdentityResolved>().Identity;
        identity.Evidence.ShouldBe(assertion.Evidence);
        identity.Version.ShouldBe(fixture.ExpectedIssuerVersion);
    }

    /// <summary>Verifies policies may narrow while retaining authoritative evidence.</summary>
    [Fact]
    public async Task ResolveAsync_WhenPolicyNarrows_AcceptsNarrowedIdentity()
    {
        var fixture = CreateFixture();
        var assertion = Assertion(fixture.Now.AddMinutes(1));
        var result = await fixture.ResolveAsync(assertion, IdentityNormalizerScenario.Narrow, TestContext.Current.CancellationToken);
        var identity = result.ShouldBeOfType<IdentityResolved>().Identity;
        identity.Evidence.ShouldBe(assertion.Evidence);
        identity.Claims.Length.ShouldBe(1);
        identity.Assurance.ShouldBe(IdentityAssuranceLevel.Basic);
        identity.Version.ShouldBe(fixture.ExpectedIssuerVersion);
    }

    /// <summary>Verifies issuer and narrowing-policy violations produce safe typed rejection.</summary>
    /// <param name="scenario">The violated normalization boundary.</param>
    /// <param name="expected">The stable expected failure classification.</param>
    [Theory]
    [InlineData(IdentityNormalizerScenario.IssuerUnavailable, IdentityFailureKind.Unavailable)]
    [InlineData(IdentityNormalizerScenario.MalformedIssuer, IdentityFailureKind.Malformed)]
    [InlineData(IdentityNormalizerScenario.ReplaceEvidence, IdentityFailureKind.Unavailable)]
    [InlineData(IdentityNormalizerScenario.ReplaceVersion, IdentityFailureKind.Unavailable)]
    [InlineData(IdentityNormalizerScenario.WidenClaims, IdentityFailureKind.Unavailable)]
    [InlineData(IdentityNormalizerScenario.WidenAssurance, IdentityFailureKind.Unavailable)]
    [InlineData(IdentityNormalizerScenario.ReplaceTenant, IdentityFailureKind.Unavailable)]
    [InlineData(IdentityNormalizerScenario.ReplacePrincipal, IdentityFailureKind.Unavailable)]
    [InlineData(IdentityNormalizerScenario.ReplaceSubjectKind, IdentityFailureKind.Unavailable)]
    [InlineData(IdentityNormalizerScenario.ReplaceDelegationChain, IdentityFailureKind.Unavailable)]
    [InlineData(IdentityNormalizerScenario.RestoreRemovedClaim, IdentityFailureKind.Unavailable)]
    public async Task ResolveAsync_WhenBoundaryIsViolated_RejectsSafely(
        IdentityNormalizerScenario scenario,
        IdentityFailureKind expected)
    {
        var fixture = CreateFixture();
        var result = await fixture.ResolveAsync(Assertion(fixture.Now.AddMinutes(1)), scenario, TestContext.Current.CancellationToken);
        var failure = result.ShouldBeOfType<IdentityRejected>().Failure;
        failure.Kind.ShouldBe(expected);
        failure.SafeMessage.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>Verifies the evidence expiry instant is exclusive.</summary>
    [Fact]
    public async Task ResolveAsync_WhenExpiryEqualsCurrentTime_RejectsExpired()
    {
        var fixture = CreateFixture();
        var result = await fixture.ResolveAsync(Assertion(fixture.Now), IdentityNormalizerScenario.Valid, TestContext.Current.CancellationToken);
        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.Expired);
    }

    /// <summary>Verifies caller cancellation propagates rather than becoming rejection.</summary>
    [Fact]
    public async Task ResolveAsync_WhenCancelled_PropagatesCancellation()
    {
        var fixture = CreateFixture();
        using var cancellation = new CancellationTokenSource();
        var pending = fixture.ResolveAsync(Assertion(fixture.Now.AddMinutes(1)), IdentityNormalizerScenario.BlockingIssuer, cancellation.Token).AsTask();
        var entered = fixture.WaitUntilBlockedAsync(TestContext.Current.CancellationToken).AsTask();
        try
        {
            (await Task.WhenAny(pending, entered)).ShouldBe(entered);
            await entered;
            await cancellation.CancelAsync();
            _ = await Should.ThrowAsync<OperationCanceledException>(async () => await pending);
        }
        finally
        {
            await cancellation.CancelAsync();
            try
            {
                _ = await pending;
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
        }
    }

    private static IdentityAssertion Assertion(DateTimeOffset expiresAt)
    {
        var issuer = new IdentityIssuerId("issuer");
        var evidence = new AuthenticationEvidence(new AuthenticationEvidenceId("evidence"), issuer, "test", expiresAt.AddMinutes(-1), expiresAt, new AuthenticationEvidenceFingerprint(new ContentHash("safe")));
        return new IdentityAssertion(issuer, "subject", [], evidence);
    }
}
