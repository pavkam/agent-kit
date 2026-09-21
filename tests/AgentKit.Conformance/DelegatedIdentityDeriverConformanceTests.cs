// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Defines reusable delegated-identity deriver contract cases.</summary>
/// <typeparam name="TFixture">The implementation fixture composed for each case.</typeparam>
public abstract class DelegatedIdentityDeriverConformanceTests<TFixture>
    where TFixture : IDelegatedIdentityDeriverConformanceFixture
{
    /// <summary>Creates an isolated deterministic fixture.</summary>
    /// <returns>The fixture for one contract case.</returns>
    protected abstract TFixture CreateFixture();

    /// <summary>Verifies narrowed claims and assurance succeed while retaining tenant and principal.</summary>
    [Fact]
    public async Task DeriveAsync_WhenClaimsAndAssuranceNarrow_ReturnsChildIdentity()
    {
        var fixture = CreateFixture();
        var parent = fixture.CreateParentIdentity();
        var result = await fixture.DeriveAsync(
            DelegatedIdentityDeriverConformanceScenario.ValidNarrow,
            new DelegatedIdentityRequest(
                new DelegationId(Guid.NewGuid()),
                parent,
                [parent.Claims[1]],
                IdentityAssuranceLevel.Basic),
            TestContext.Current.CancellationToken);
        var child = result.ShouldBeOfType<IdentityResolved>().Identity;
        child.TenantId.ShouldBe(parent.TenantId);
        child.PrincipalId.ShouldBe(parent.PrincipalId);
        child.Claims.ShouldBe([parent.Claims[1]]);
        child.Assurance.ShouldBe(IdentityAssuranceLevel.Basic);
        child.DelegationChain.Length.ShouldBe(parent.DelegationChain.Length + 1);
    }

    /// <summary>Verifies retained delegation history cannot broaden claims or assurance.</summary>
    [Fact]
    public async Task DeriveAsync_WhenRetainedHistoryBroadensClaims_RejectsDelegationWouldBroaden()
    {
        var fixture = CreateFixture();
        var retained = new IdentityClaim(new IdentityIssuerId("issuer"), "scope", "read", IdentityClaimValueKind.Text);
        var added = new IdentityClaim(new IdentityIssuerId("issuer"), "role", "admin", IdentityClaimValueKind.Text);
        var evidence = fixture.CreateParentIdentity().Evidence;
        var first = new DelegationIdentityLink(
            new DelegationId(Guid.NewGuid()),
            new TenantId("tenant"),
            new PrincipalId("principal"),
            evidence.Issuer,
            evidence.Id,
            new IdentityVersion(1),
            fixture.Now,
            [retained],
            IdentityAssuranceLevel.Basic);
        var second = new DelegationIdentityLink(
            new DelegationId(Guid.NewGuid()),
            new TenantId("tenant"),
            new PrincipalId("principal"),
            evidence.Issuer,
            evidence.Id,
            new IdentityVersion(1),
            fixture.Now,
            [retained, added],
            IdentityAssuranceLevel.Basic);
        var parent = new ExecutionIdentity(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Human,
            evidence,
            second.Claims,
            [first, second],
            second.Assurance,
            new IdentityVersion(1));
        var result = await fixture.DeriveAsync(
            DelegatedIdentityDeriverConformanceScenario.ValidNarrow,
            new DelegatedIdentityRequest(
                new DelegationId(Guid.NewGuid()),
                parent,
                parent.Claims,
                parent.Assurance),
            TestContext.Current.CancellationToken);
        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.DelegationWouldBroaden);
    }

    /// <summary>Verifies configured delegation depth is enforced.</summary>
    [Fact]
    public async Task DeriveAsync_WhenParentIsAtMaximumDepth_RejectsDelegationWouldBroaden()
    {
        var fixture = CreateFixture();
        var parent = fixture.CreateParentIdentity();
        var atDepth = await fixture.DeriveAsync(
            DelegatedIdentityDeriverConformanceScenario.ValidNarrow,
            new DelegatedIdentityRequest(
                new DelegationId(Guid.NewGuid()),
                parent,
                parent.Claims,
                parent.Assurance),
            TestContext.Current.CancellationToken);
        var chainedParent = atDepth.ShouldBeOfType<IdentityResolved>().Identity;
        var result = await fixture.DeriveAsync(
            DelegatedIdentityDeriverConformanceScenario.MaximumDepth,
            new DelegatedIdentityRequest(
                new DelegationId(Guid.NewGuid()),
                chainedParent,
                chainedParent.Claims,
                chainedParent.Assurance),
            TestContext.Current.CancellationToken);
        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.DelegationWouldBroaden);
    }

    /// <summary>Verifies expired parent evidence rejects before creating a child.</summary>
    [Fact]
    public async Task DeriveAsync_WhenParentEvidenceIsExpired_RejectsExpired()
    {
        var fixture = CreateFixture();
        var parent = fixture.CreateParentIdentity();
        var result = await fixture.DeriveAsync(
            DelegatedIdentityDeriverConformanceScenario.ExpiredParent,
            new DelegatedIdentityRequest(
                new DelegationId(Guid.NewGuid()),
                parent,
                parent.Claims,
                parent.Assurance),
            TestContext.Current.CancellationToken);
        result.ShouldBeOfType<IdentityRejected>().Failure.Kind.ShouldBe(IdentityFailureKind.Expired);
    }

    /// <summary>Verifies caller cancellation propagates rather than becoming rejection.</summary>
    [Fact]
    public async Task DeriveAsync_WhenCancelled_PropagatesCancellation()
    {
        var fixture = CreateFixture();
        using var cancellation = new CancellationTokenSource();
        var parent = fixture.CreateParentIdentity();
        var pending = fixture.DeriveAsync(
            DelegatedIdentityDeriverConformanceScenario.BlockingDerivation,
            new DelegatedIdentityRequest(
                new DelegationId(Guid.NewGuid()),
                parent,
                parent.Claims,
                parent.Assurance),
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
}
