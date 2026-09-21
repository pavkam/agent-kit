// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Defines reusable trusted-ingress issuer contract cases.</summary>
/// <typeparam name="TFixture">The implementation fixture composed for each case.</typeparam>
public abstract class IdentityIssuerConformanceTests<TFixture>
    where TFixture : IIdentityIssuerConformanceFixture
{
    /// <summary>Creates an isolated deterministic fixture.</summary>
    /// <returns>The fixture for one contract case.</returns>
    protected abstract TFixture CreateFixture();

    /// <summary>Verifies non-revoked evidence is accepted.</summary>
    [Fact]
    public async Task ValidateEvidenceAsync_WhenEvidenceIsValid_ReturnsPassed()
    {
        var fixture = CreateFixture();
        var evidence = Evidence(fixture.Now.AddMinutes(-5), fixture.Now.AddMinutes(5));
        var result = await fixture.ValidateEvidenceAsync(
            IdentityIssuerConformanceScenario.Valid,
            evidence,
            fixture.Now,
            TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<IdentityValidationPassed>();
    }

    /// <summary>Verifies revoked evidence is rejected safely.</summary>
    [Fact]
    public async Task ValidateEvidenceAsync_WhenEvidenceIsRevoked_RejectsRevoked()
    {
        var fixture = CreateFixture();
        var evidence = Evidence(fixture.Now.AddMinutes(-5), fixture.Now.AddMinutes(5));
        var result = await fixture.ValidateEvidenceAsync(
            IdentityIssuerConformanceScenario.Revoked,
            evidence,
            fixture.Now,
            TestContext.Current.CancellationToken);
        var rejected = result.ShouldBeOfType<IdentityValidationRejected>();
        rejected.Failure.Kind.ShouldBe(IdentityFailureKind.Revoked);
        rejected.Failure.SafeMessage.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>Verifies caller cancellation propagates rather than becoming rejection.</summary>
    [Fact]
    public async Task ValidateEvidenceAsync_WhenCancelled_PropagatesCancellation()
    {
        var fixture = CreateFixture();
        using var cancellation = new CancellationTokenSource();
        var evidence = Evidence(fixture.Now.AddMinutes(-5), fixture.Now.AddMinutes(5));
        var pending = fixture.ValidateEvidenceAsync(
            IdentityIssuerConformanceScenario.BlockingValidation,
            evidence,
            fixture.Now,
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

    private static AuthenticationEvidence Evidence(DateTimeOffset authenticatedAt, DateTimeOffset expiresAt) =>
        new(
            new AuthenticationEvidenceId("evidence"),
            new IdentityIssuerId("issuer"),
            "test",
            authenticatedAt,
            expiresAt,
            new AuthenticationEvidenceFingerprint(new ContentHash("safe")));
}
