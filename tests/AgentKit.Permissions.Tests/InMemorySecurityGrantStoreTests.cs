// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

public sealed class InMemorySecurityGrantStoreTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidateAndConsumeAsync_WhenEvidenceMatches_ConsumesOneUse()
    {
        var clock = new FakeTimeProvider(_now);
        var store = new InMemorySecurityGrantStore(clock);
        var grant = CreateGrant(allowedUses: 2);
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);

        var result = await store.ValidateAndConsumeAsync(grant, CreateEnforcement(grant), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(GrantConsumptionStatus.Consumed);
        result.RemainingUses.ShouldBe(1);
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WhenResourceDiffers_DoesNotConsumeUse()
    {
        var store = new InMemorySecurityGrantStore(new FakeTimeProvider(_now));
        var grant = CreateGrant();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        var mismatched = CreateEnforcement(grant) with
        {
            Resources = [new ProtectedResource(ProtectedResourceKind.File, "/workspace/other.txt")],
        };

        var mismatch = await store.ValidateAndConsumeAsync(grant, mismatched, TestContext.Current.CancellationToken);
        var valid = await store.ValidateAndConsumeAsync(grant, CreateEnforcement(grant), TestContext.Current.CancellationToken);

        mismatch.Status.ShouldBe(GrantConsumptionStatus.Mismatch);
        mismatch.RemainingUses.ShouldBe(1);
        valid.Status.ShouldBe(GrantConsumptionStatus.Consumed);
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WhenGrantExpired_DeniesWithoutConsumption()
    {
        var clock = new FakeTimeProvider(_now);
        var store = new InMemorySecurityGrantStore(clock);
        var grant = CreateGrant();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(11));

        var result = await store.ValidateAndConsumeAsync(grant, CreateEnforcement(grant), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(GrantConsumptionStatus.Expired);
        result.RemainingUses.ShouldBe(1);
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WhenRevocationEpochChanges_DeniesAsRevoked()
    {
        var store = new InMemorySecurityGrantStore(new FakeTimeProvider(_now));
        var grant = CreateGrant();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        var enforcement = CreateEnforcement(grant) with { RevocationVersion = new SecurityRevocationVersion(2) };

        var result = await store.ValidateAndConsumeAsync(grant, enforcement, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(GrantConsumptionStatus.Revoked);
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WhenConcurrentSingleUse_AllowsExactlyOneConsumer()
    {
        var store = new InMemorySecurityGrantStore(new FakeTimeProvider(_now));
        var grant = CreateGrant();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        var enforcement = CreateEnforcement(grant);

        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(
            async _ => await store.ValidateAndConsumeAsync(grant, enforcement, TestContext.Current.CancellationToken)));

        results.Count(static result => result.Status == GrantConsumptionStatus.Consumed).ShouldBe(1);
        results.Count(static result => result.Status == GrantConsumptionStatus.Exhausted).ShouldBe(31);
    }

    [Fact]
    public async Task RevokeAsync_WhenKnown_PreventsFutureConsumption()
    {
        var store = new InMemorySecurityGrantStore(new FakeTimeProvider(_now));
        var grant = CreateGrant();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);

        var revoked = await store.RevokeAsync(grant.Id, TestContext.Current.CancellationToken);
        var result = await store.ValidateAndConsumeAsync(grant, CreateEnforcement(grant), TestContext.Current.CancellationToken);

        revoked.ShouldBeTrue();
        result.Status.ShouldBe(GrantConsumptionStatus.Revoked);
    }

    internal static SecurityGrant CreateGrantForTests(int allowedUses = 1) => CreateGrant(allowedUses);

    private static SecurityGrant CreateGrant(int allowedUses = 1)
    {
        var scope = new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
            new InRunOperationCorrelation(
                new OperationId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
                new RunId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
                null));
        var identity = new ExecutionIdentity(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Human,
            ExtensionData.Empty);

        return new SecurityGrant(
            new GrantId(Guid.Parse("50000000-0000-0000-0000-000000000005")),
            new SecurityRequestId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
            scope,
            identity,
            new ComponentId("filesystem"),
            SecurityOperationKind.FileRead,
            SecurityEffect.Observe,
            [new ProtectedResource(ProtectedResourceKind.File, "/workspace/file.txt")],
            new InputFingerprint("sha256:abc"),
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            _now.AddMinutes(-1),
            _now.AddMinutes(10),
            allowedUses);
    }

    private static SecurityEnforcementRequest CreateEnforcement(SecurityGrant grant) => new(
        grant.Scope,
        grant.Identity,
        grant.Audience,
        grant.Kind,
        grant.Effect,
        grant.Resources,
        grant.InputFingerprint,
        grant.RevocationVersion);
}
