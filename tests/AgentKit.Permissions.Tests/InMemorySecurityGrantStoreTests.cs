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
    public async Task ValidateAndConsumeAsync_WhenPresentedGrantEvidenceDiffers_DoesNotConsumeUse()
    {
        var store = new InMemorySecurityGrantStore(new FakeTimeProvider(_now));
        var grant = CreateGrant();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        var tampered = grant with { Effect = SecurityEffect.Delete };

        var rejected = await store.ValidateAndConsumeAsync(
            tampered,
            CreateEnforcement(grant),
            TestContext.Current.CancellationToken);
        var valid = await store.ValidateAndConsumeAsync(grant, CreateEnforcement(grant), TestContext.Current.CancellationToken);

        rejected.Status.ShouldBe(GrantConsumptionStatus.Tampered);
        rejected.RemainingUses.ShouldBe(1);
        valid.Status.ShouldBe(GrantConsumptionStatus.Consumed);
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WhenTamperedEvidenceIsPresentedConcurrently_DoesNotConsumeUse()
    {
        var store = new InMemorySecurityGrantStore(new FakeTimeProvider(_now));
        var grant = CreateGrant();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        var tampered = grant with { Effect = SecurityEffect.Delete };

        var results = await ConsumeConcurrentlyAsync(store, tampered, CreateEnforcement(grant));
        var valid = await store.ValidateAndConsumeAsync(grant, CreateEnforcement(grant), TestContext.Current.CancellationToken);

        results.ShouldAllBe(static result => result.Status == GrantConsumptionStatus.Tampered);
        results.ShouldAllBe(static result => result.RemainingUses == 1);
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

        var results = await ConsumeConcurrentlyAsync(store, grant, enforcement);

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

    [Fact]
    public async Task ValidateAndConsumeAsync_WhenRevokedBeforeConcurrentConsumers_AllAreRevoked()
    {
        var store = new InMemorySecurityGrantStore(new FakeTimeProvider(_now));
        var grant = CreateGrant();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        _ = await store.RevokeAsync(grant.Id, TestContext.Current.CancellationToken);

        var results = await ConsumeConcurrentlyAsync(store, grant, CreateEnforcement(grant));

        results.ShouldAllBe(static result => result.Status == GrantConsumptionStatus.Revoked);
        results.ShouldAllBe(static result => result.RemainingUses == 1);
    }

    internal static SecurityGrant CreateGrantForTests(int allowedUses = 1) => CreateGrant(allowedUses);

    /// <summary>Releases a fixed worker set together so every call contends for the grant store's synchronization boundary.</summary>
    /// <param name="store">The store whose atomic consumption behavior is under test.</param>
    /// <param name="grant">The grant evidence each worker presents.</param>
    /// <param name="enforcement">The exact enforcement evidence each worker presents.</param>
    /// <returns>The terminal result for every concurrently released worker.</returns>
    private static async Task<GrantConsumptionResult[]> ConsumeConcurrentlyAsync(
        InMemorySecurityGrantStore store,
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement)
    {
        const int workerCount = 32;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationToken = TestContext.Current.CancellationToken;
        var arrivals = 0;
        var workers = Enumerable.Range(0, workerCount).Select(_ => Task.Run(async () =>
        {
            if (Interlocked.Increment(ref arrivals) == workerCount)
            {
                start.SetResult();
            }

            await start.Task;
            return await store.ValidateAndConsumeAsync(
                grant,
                enforcement,
                cancellationToken);
        }));

        return await Task.WhenAll(workers);
    }

    private static SecurityGrant CreateGrant(int allowedUses = 1)
    {
        var scope = new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
            new InRunOperationCorrelation(
                new OperationId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
                new RunId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
                null));
        var identity = AgentKit.TestSupport.TestExecutionIdentity.Create(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Human);

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
