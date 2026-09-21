// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Defines reusable identity validation-policy contract cases.</summary>
/// <typeparam name="TFixture">The implementation fixture composed for each case.</typeparam>
public abstract class IdentityValidationPolicyConformanceTests<TFixture>
    where TFixture : IIdentityValidationPolicyConformanceFixture
{
    /// <summary>Creates an isolated deterministic fixture.</summary>
    /// <returns>The fixture for one contract case.</returns>
    protected abstract TFixture CreateFixture();

    /// <summary>Verifies valid evidence passes policy checks.</summary>
    [Fact]
    public async Task ValidateAsync_WhenEvidenceIsWithinBounds_ReturnsPassed()
    {
        var fixture = CreateFixture();
        var identity = Identity(
            Evidence(fixture.Now.AddMinutes(-5), fixture.Now.AddMinutes(30)),
            fixture.Now);
        var result = await fixture.ValidateAsync(
            IdentityValidationPolicyConformanceScenario.Valid,
            identity,
            TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<IdentityValidationPassed>();
    }

    /// <summary>Verifies the evidence expiry instant is exclusive.</summary>
    [Fact]
    public async Task ValidateAsync_WhenExpiryEqualsCurrentTime_RejectsExpired()
    {
        var fixture = CreateFixture();
        var identity = Identity(
            Evidence(fixture.Now.AddMinutes(-5), fixture.Now),
            fixture.Now);
        var result = await fixture.ValidateAsync(
            IdentityValidationPolicyConformanceScenario.ExpiredAtEquality,
            identity,
            TestContext.Current.CancellationToken);
        result.ShouldBeOfType<IdentityValidationRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.Expired);
    }

    /// <summary>Verifies clock skew does not forgive future authentication beyond the bound.</summary>
    [Fact]
    public async Task ValidateAsync_WhenAuthenticatedAtIsBeyondClockSkew_RejectsMalformed()
    {
        var fixture = CreateFixture();
        var identity = Identity(
            Evidence(fixture.Now.Add(fixture.MaximumClockSkew).AddMinutes(1), fixture.Now.AddHours(1)),
            fixture.Now);
        var result = await fixture.ValidateAsync(
            IdentityValidationPolicyConformanceScenario.FutureAuthenticatedAt,
            identity,
            TestContext.Current.CancellationToken);
        result.ShouldBeOfType<IdentityValidationRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.Malformed);
    }

    /// <summary>Verifies configured maximum evidence lifetime is enforced.</summary>
    [Fact]
    public async Task ValidateAsync_WhenLifetimeExceeded_RejectsExpired()
    {
        var fixture = CreateFixture();
        var authenticatedAt = fixture.Now - fixture.MaximumEvidenceLifetime;
        var identity = Identity(Evidence(authenticatedAt, null), fixture.Now);
        var result = await fixture.ValidateAsync(
            IdentityValidationPolicyConformanceScenario.LifetimeExceeded,
            identity,
            TestContext.Current.CancellationToken);
        result.ShouldBeOfType<IdentityValidationRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.Expired);
    }

    /// <summary>Verifies caller cancellation propagates rather than becoming rejection.</summary>
    [Fact]
    public async Task ValidateAsync_WhenCancelled_PropagatesCancellation()
    {
        var fixture = CreateFixture();
        using var cancellation = new CancellationTokenSource();
        var identity = Identity(
            Evidence(fixture.Now.AddMinutes(-5), fixture.Now.AddMinutes(30)),
            fixture.Now);
        var pending = fixture.ValidateAsync(
            IdentityValidationPolicyConformanceScenario.BlockingValidation,
            identity,
            cancellation.Token).AsTask();
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

    private static ExecutionIdentity Identity(AuthenticationEvidence evidence, DateTimeOffset _) => new(
        new TenantId("tenant"),
        new PrincipalId("principal"),
        ExecutionSubjectKind.Human,
        evidence,
        [],
        [],
        IdentityAssuranceLevel.Basic,
        new IdentityVersion(1));

    private static AuthenticationEvidence Evidence(DateTimeOffset authenticatedAt, DateTimeOffset? expiresAt) =>
        new(
            new AuthenticationEvidenceId("evidence"),
            new IdentityIssuerId("issuer"),
            "test",
            authenticatedAt,
            expiresAt,
            new AuthenticationEvidenceFingerprint(new ContentHash("safe")));
}
