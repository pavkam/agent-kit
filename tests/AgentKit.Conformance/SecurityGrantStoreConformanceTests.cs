// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Defines reusable, mandatory behavioral cases for implementations of <see cref="ISecurityGrantStore"/>.</summary>
/// <typeparam name="TFixture">The implementation fixture that composes a store and supplies its deterministic clock.</typeparam>
public abstract class SecurityGrantStoreConformanceTests<TFixture>
    where TFixture : ISecurityGrantStoreConformanceFixture
{
    /// <summary>Creates a fresh fixture so each contract case has isolated authoritative grant state.</summary>
    /// <returns>The fixture used by one conformance case.</returns>
    protected abstract TFixture CreateFixture();

    /// <summary>Verifies identical registration replay is accepted while different evidence under the same identifier is rejected.</summary>
    [Fact]
    public async Task RegisterAsync_WhenEvidenceIsReplayedOrConflicts_PreservesOriginalEvidence()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var grant = CreateGrant(fixture.TimeProvider.GetUtcNow());

        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await store.RegisterAsync(
            grant with { Effect = SecurityEffect.Delete },
            TestContext.Current.CancellationToken));

        var result = await store.ValidateAndConsumeAsync(grant, CreateEnforcement(grant), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GrantConsumptionStatus.Consumed);
    }

    /// <summary>Verifies exact enforcement evidence is required and rejected evidence leaves capacity unchanged.</summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_WhenEnforcementDiffers_RejectsWithoutConsumingCapacity()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var grant = CreateGrant(fixture.TimeProvider.GetUtcNow(), allowedUses: 2);
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);

        var mismatch = await store.ValidateAndConsumeAsync(
            grant,
            CreateEnforcement(grant) with { Resources = [new ProtectedResource(ProtectedResourceKind.File, "/workspace/other.txt")] },
            TestContext.Current.CancellationToken);
        var valid = await store.ValidateAndConsumeAsync(grant, CreateEnforcement(grant), TestContext.Current.CancellationToken);

        mismatch.Status.ShouldBe(GrantConsumptionStatus.Mismatch);
        mismatch.RemainingUses.ShouldBe(2);
        valid.Status.ShouldBe(GrantConsumptionStatus.Consumed);
        valid.RemainingUses.ShouldBe(1);
    }

    /// <summary>Verifies a validly shaped grant with altered immutable evidence is rejected without consuming the registered grant.</summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_WhenGrantEvidenceIsTampered_RejectsWithoutConsumingCapacity()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var grant = CreateGrant(fixture.TimeProvider.GetUtcNow());
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);

        var tampered = await store.ValidateAndConsumeAsync(
            grant with { Effect = SecurityEffect.Delete },
            CreateEnforcement(grant),
            TestContext.Current.CancellationToken);
        var valid = await store.ValidateAndConsumeAsync(grant, CreateEnforcement(grant), TestContext.Current.CancellationToken);

        tampered.Status.ShouldBe(GrantConsumptionStatus.Tampered);
        tampered.RemainingUses.ShouldBe(1);
        valid.Status.ShouldBe(GrantConsumptionStatus.Consumed);
    }

    /// <summary>Verifies expiry evaluated from the fixture clock blocks future consumption without spending capacity.</summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_WhenGrantExpires_RejectsWithoutConsumingCapacity()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var grant = CreateGrant(fixture.TimeProvider.GetUtcNow(), lifetime: TimeSpan.FromMinutes(1));
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        fixture.Advance(TimeSpan.FromMinutes(1));

        var result = await store.ValidateAndConsumeAsync(grant, CreateEnforcement(grant), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(GrantConsumptionStatus.Expired);
        result.RemainingUses.ShouldBe(1);
    }

    /// <summary>Verifies a cancelled consumption attempt has no effect on the registered grant's remaining capacity.</summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_WhenCancelledBeforeConsumption_DoesNotConsumeCapacity()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var grant = CreateGrant(fixture.TimeProvider.GetUtcNow());
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await store.ValidateAndConsumeAsync(
            grant,
            CreateEnforcement(grant),
            cancellation.Token));
        var valid = await store.ValidateAndConsumeAsync(grant, CreateEnforcement(grant), TestContext.Current.CancellationToken);

        valid.Status.ShouldBe(GrantConsumptionStatus.Consumed);
        valid.RemainingUses.ShouldBe(0);
    }

    /// <summary>Verifies documented null caller arguments fail before they can affect a valid registered grant.</summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_WhenRequiredArgumentsAreNull_ThrowsBeforeConsumingCapacity()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var grant = CreateGrant(fixture.TimeProvider.GetUtcNow());
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);

        var grantException = await Should.ThrowAsync<ArgumentNullException>(async () => await store.ValidateAndConsumeAsync(
            null!,
            CreateEnforcement(grant),
            TestContext.Current.CancellationToken));
        var enforcementException = await Should.ThrowAsync<ArgumentNullException>(async () => await store.ValidateAndConsumeAsync(
            grant,
            null!,
            TestContext.Current.CancellationToken));
        var valid = await store.ValidateAndConsumeAsync(grant, CreateEnforcement(grant), TestContext.Current.CancellationToken);

        grantException.ParamName.ShouldBe("grant");
        enforcementException.ParamName.ShouldBe("enforcement");
        valid.Status.ShouldBe(GrantConsumptionStatus.Consumed);
    }

    /// <summary>Verifies simultaneous consumers cannot spend a single grant use more than once.</summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_WhenSingleUseConsumersRace_ConsumesExactlyOneUse()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var grant = CreateGrant(fixture.TimeProvider.GetUtcNow());
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);

        var results = await ConsumeConcurrentlyAsync(store, grant, CreateEnforcement(grant));

        results.Count(static result => result.Status == GrantConsumptionStatus.Consumed).ShouldBe(1);
        results.Count(static result => result.Status == GrantConsumptionStatus.Exhausted).ShouldBe(31);
    }

    /// <summary>Verifies an identical enforcement intent returns historical evidence without authorizing another effect.</summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_WhenIntentIsReplayed_ReconcilesWithoutAnotherConsumption()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var grant = CreateGrant(fixture.TimeProvider.GetUtcNow());
        var enforcement = CreateEnforcement(grant);
        var intent = CreateIntent();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);

        var consumed = await store.ValidateAndConsumeAsync(
            grant, enforcement, intent, TestContext.Current.CancellationToken);
        var reconciled = await store.ValidateAndConsumeAsync(
            grant, enforcement, intent, TestContext.Current.CancellationToken);

        consumed.Status.ShouldBe(GrantConsumptionStatus.Consumed);
        _ = consumed.IntentReceipt.ShouldNotBeNull();
        reconciled.Status.ShouldBe(GrantConsumptionStatus.Reconciled);
        reconciled.IntentReceipt.ShouldBe(consumed.IntentReceipt);
        reconciled.RemainingUses.ShouldBe(consumed.RemainingUses);
    }

    /// <summary>Verifies simultaneous identical intents produce exactly one fresh authority and receipt-only reconciliation for every loser.</summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_WhenIdenticalIntentsRace_AuthorizesExactlyOneEffect()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var grant = CreateGrant(fixture.TimeProvider.GetUtcNow());
        var enforcement = CreateEnforcement(grant);
        var intent = CreateIntent();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);

        var results = await ConsumeIntentConcurrentlyAsync(store, grant, enforcement, intent);

        results.Count(static result => result.Status == GrantConsumptionStatus.Consumed).ShouldBe(1);
        results.Count(static result => result.Status == GrantConsumptionStatus.Reconciled).ShouldBe(31);
        results.ShouldAllBe(static result => result.IntentReceipt != null);
        results.Select(static result => result.IntentReceipt).Distinct().Count().ShouldBe(1);
    }

    /// <summary>Verifies one intent identity cannot be rebound to changed concrete effect or fencing evidence.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ValidateAndConsumeAsync_WhenIntentEvidenceChanges_RejectsWithoutAnotherConsumption(bool changeEffect)
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var grant = CreateGrant(fixture.TimeProvider.GetUtcNow(), allowedUses: 2);
        var enforcement = CreateEnforcement(grant);
        var intent = CreateIntent();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        _ = await store.ValidateAndConsumeAsync(
            grant, enforcement, intent, TestContext.Current.CancellationToken);
        var changedEnforcement = changeEffect
            ? enforcement with { InputFingerprint = new InputFingerprint("sha256:changed") }
            : enforcement;
        var changedIntent = changeEffect
            ? intent
            : new SecurityEnforcementIntent(intent.Id, new FencingToken(7));

        var mismatch = await store.ValidateAndConsumeAsync(
            grant, changedEnforcement, changedIntent, TestContext.Current.CancellationToken);
        var remaining = await store.ValidateAndConsumeAsync(
            grant, enforcement, CreateIntent(2), TestContext.Current.CancellationToken);

        mismatch.Status.ShouldBe(GrantConsumptionStatus.Mismatch);
        mismatch.RemainingUses.ShouldBe(1);
        mismatch.IntentReceipt.ShouldBeNull();
        remaining.Status.ShouldBe(GrantConsumptionStatus.Consumed);
        remaining.RemainingUses.ShouldBe(0);
    }

    /// <summary>Verifies expiry or revocation does not turn historical receipt recovery into renewed effect authority.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ValidateAndConsumeAsync_WhenConsumedIntentBecomesInvalid_ReconcilesHistoryButRejectsFreshIntent(bool revoke)
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var grant = CreateGrant(fixture.TimeProvider.GetUtcNow(), lifetime: TimeSpan.FromMinutes(1));
        var enforcement = CreateEnforcement(grant);
        var intent = CreateIntent();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        var consumed = await store.ValidateAndConsumeAsync(
            grant, enforcement, intent, TestContext.Current.CancellationToken);
        if (revoke)
        {
            _ = await store.RevokeAsync(grant.Id, TestContext.Current.CancellationToken);
        }
        else
        {
            fixture.Advance(TimeSpan.FromMinutes(1));
        }

        var historical = await store.ValidateAndConsumeAsync(
            grant, enforcement, intent, TestContext.Current.CancellationToken);
        var fresh = await store.ValidateAndConsumeAsync(
            grant, enforcement, CreateIntent(2), TestContext.Current.CancellationToken);

        historical.Status.ShouldBe(GrantConsumptionStatus.Reconciled);
        historical.IntentReceipt.ShouldBe(consumed.IntentReceipt);
        fresh.Status.ShouldBe(revoke ? GrantConsumptionStatus.Revoked : GrantConsumptionStatus.Expired);
        fresh.IntentReceipt.ShouldBeNull();
    }

    /// <summary>Verifies the additive default overload fails closed without delegating to a legacy consumption effect.</summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_WhenIntentReceiptsAreUnsupported_DoesNotInvokeLegacyConsumption()
    {
        ISecurityGrantStore store = new LegacyOnlyGrantStore();
        var grant = CreateGrant(DateTimeOffset.UnixEpoch);

        var result = await store.ValidateAndConsumeAsync(
            grant, CreateEnforcement(grant), CreateIntent(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(GrantConsumptionStatus.Unknown);
        result.IntentReceipt.ShouldBeNull();
        ((LegacyOnlyGrantStore) store).LegacyConsumptionCalls.ShouldBe(0);
    }

    /// <summary>Verifies revocation is idempotent and prevents every later grant consumption.</summary>
    [Fact]
    public async Task RevokeAsync_WhenGrantIsKnown_PreventsFutureConsumption()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var grant = CreateGrant(fixture.TimeProvider.GetUtcNow());
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);

        (await store.RevokeAsync(grant.Id, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await store.RevokeAsync(grant.Id, TestContext.Current.CancellationToken)).ShouldBeTrue();
        var result = await store.ValidateAndConsumeAsync(grant, CreateEnforcement(grant), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(GrantConsumptionStatus.Revoked);
        result.RemainingUses.ShouldBe(1);
    }

    /// <summary>Creates deterministic, valid grant evidence for a contract case.</summary>
    /// <param name="now">The fixture-controlled issue instant.</param>
    /// <param name="allowedUses">The number of authorized consumptions.</param>
    /// <param name="lifetime">The exclusive validity duration.</param>
    /// <returns>A grant whose evidence can be consumed by <see cref="CreateEnforcement"/>.</returns>
    private static SecurityGrant CreateGrant(DateTimeOffset now, int allowedUses = 1, TimeSpan? lifetime = null)
    {
        var scope = new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
            new InRunOperationCorrelation(
                new OperationId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
                new RunId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
                null));
        var identity = TestSupport.TestExecutionIdentity.Create(
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
            now,
            now + (lifetime ?? TimeSpan.FromMinutes(10)),
            allowedUses);
    }

    /// <summary>Creates enforcement evidence matching all immutable evidence in a test grant.</summary>
    /// <param name="grant">The grant whose effect will be enforced.</param>
    /// <returns>Exact enforcement evidence for the grant.</returns>
    private static SecurityEnforcementRequest CreateEnforcement(SecurityGrant grant) => new(
        grant.Scope,
        grant.Identity,
        grant.Audience,
        grant.Kind,
        grant.Effect,
        grant.Resources,
        grant.InputFingerprint,
        grant.RevocationVersion);

    /// <summary>Creates stable enforcement-intent evidence for receipt conformance cases.</summary>
    /// <param name="discriminator">The positive deterministic identity suffix.</param>
    /// <returns>A process-local intent with a non-default stable identity.</returns>
    private static SecurityEnforcementIntent CreateIntent(int discriminator = 1) => new(
        new SecurityEnforcementIntentId(Guid.Parse($"70000000-0000-0000-0000-{discriminator:D12}")),
        null);

    /// <summary>Releases fixed workers together so each implementation faces real competing consumption calls.</summary>
    /// <param name="store">The store to exercise.</param>
    /// <param name="grant">The evidence every worker presents.</param>
    /// <param name="enforcement">The exact effect evidence every worker presents.</param>
    /// <returns>The terminal result from every worker.</returns>
    private static async Task<GrantConsumptionResult[]> ConsumeConcurrentlyAsync(
        ISecurityGrantStore store,
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

            await start.Task.WaitAsync(cancellationToken);
            return await store.ValidateAndConsumeAsync(grant, enforcement, cancellationToken);
        }));

        return await Task.WhenAll(workers).WaitAsync(cancellationToken);
    }

    /// <summary>Releases fixed workers with one identical receipt-bearing intent.</summary>
    /// <param name="store">The store under test.</param>
    /// <param name="grant">The registered single-use grant.</param>
    /// <param name="enforcement">The exact concrete effect.</param>
    /// <param name="intent">The exact stable intent every worker presents.</param>
    /// <returns>All terminal consumption or reconciliation results.</returns>
    private static async Task<GrantConsumptionResult[]> ConsumeIntentConcurrentlyAsync(
        ISecurityGrantStore store,
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent)
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

            await start.Task.WaitAsync(cancellationToken);
            return await store.ValidateAndConsumeAsync(grant, enforcement, intent, cancellationToken);
        }));

        return await Task.WhenAll(workers).WaitAsync(cancellationToken);
    }

    private sealed class LegacyOnlyGrantStore: ISecurityGrantStore
    {
        public int LegacyConsumptionCalls { get; private set; }

        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant,
            SecurityEnforcementRequest enforcement,
            CancellationToken cancellationToken = default)
        {
            LegacyConsumptionCalls++;
            return ValueTask.FromResult(new GrantConsumptionResult(
                GrantConsumptionStatus.Consumed, 0, "Legacy consumption was invoked."));
        }

        public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }
}
