// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.InMemory.Tests;

using System.Diagnostics.Metrics;

using AgentKit.Conformance;

using Microsoft.Extensions.Logging;

/// <summary>Verifies InMemorySecurityGrantStore behavior and contracts.</summary>
public sealed class InMemorySecurityGrantStoreTests: SecurityGrantStoreConformanceTests<InMemorySecurityGrantStoreConformanceFixture>
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
        var tampered = grant with
        {
            Effect = SecurityEffect.Delete
        };
        var rejected = await store.ValidateAndConsumeAsync(tampered, CreateEnforcement(grant), TestContext.Current.CancellationToken);
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
        var tampered = grant with
        {
            Effect = SecurityEffect.Delete
        };
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
        var enforcement = CreateEnforcement(grant) with
        {
            RevocationVersion = new SecurityRevocationVersion(2)
        };
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
    public async Task ValidateAndConsumeAsync_WhenIntentIsReplayed_ReturnsReceiptWithoutNewAuthority()
    {
        var store = new InMemorySecurityGrantStore(new FakeTimeProvider(_now));
        var grant = CreateGrant();
        var enforcement = CreateEnforcement(grant);
        var intent = CreateIntent();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        var consumed = await store.ValidateAndConsumeAsync(grant, enforcement, intent, TestContext.Current.CancellationToken);
        var replay = await store.ValidateAndConsumeAsync(grant, enforcement, intent, TestContext.Current.CancellationToken);
        consumed.Status.ShouldBe(GrantConsumptionStatus.Consumed);
        _ = consumed.IntentReceipt.ShouldNotBeNull();
        replay.Status.ShouldBe(GrantConsumptionStatus.Reconciled);
        replay.IntentReceipt.ShouldBe(consumed.IntentReceipt);
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WhenIdenticalIntentContends_AuthorizesExactlyOneConsumer()
    {
        var store = new InMemorySecurityGrantStore(new FakeTimeProvider(_now));
        var grant = CreateGrant();
        var enforcement = CreateEnforcement(grant);
        var intent = CreateIntent();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        var calls = Enumerable.Range(0, 32).Select(_ => Task.Run(async () => await store.ValidateAndConsumeAsync(grant, enforcement, intent, TestContext.Current.CancellationToken)));
        var results = await Task.WhenAll(calls);
        results.Count(static result => result.Status == GrantConsumptionStatus.Consumed).ShouldBe(1);
        results.Count(static result => result.Status == GrantConsumptionStatus.Reconciled).ShouldBe(31);
        results.ShouldAllBe(static result => result.IntentReceipt != null);
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WhenConsumedIntentIsReplayedAfterRevocation_ReturnsHistoricalReceiptOnly()
    {
        var store = new InMemorySecurityGrantStore(new FakeTimeProvider(_now));
        var grant = CreateGrant();
        var enforcement = CreateEnforcement(grant);
        var intent = CreateIntent();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        var consumed = await store.ValidateAndConsumeAsync(grant, enforcement, intent, TestContext.Current.CancellationToken);
        _ = await store.RevokeAsync(grant.Id, TestContext.Current.CancellationToken);
        var replay = await store.ValidateAndConsumeAsync(grant, enforcement, intent, TestContext.Current.CancellationToken);
        var freshIntent = await store.ValidateAndConsumeAsync(grant, enforcement, CreateIntent(2), TestContext.Current.CancellationToken);
        replay.Status.ShouldBe(GrantConsumptionStatus.Reconciled);
        replay.IntentReceipt.ShouldBe(consumed.IntentReceipt);
        freshIntent.Status.ShouldBe(GrantConsumptionStatus.Revoked);
        freshIntent.IntentReceipt.ShouldBeNull();
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WhenConsumedIntentIsReplayedAfterExpiry_ReturnsHistoricalReceiptOnly()
    {
        var clock = new FakeTimeProvider(_now);
        var store = new InMemorySecurityGrantStore(clock);
        var grant = CreateGrant();
        var enforcement = CreateEnforcement(grant);
        var intent = CreateIntent();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        _ = await store.ValidateAndConsumeAsync(grant, enforcement, intent, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(11));
        var replay = await store.ValidateAndConsumeAsync(grant, enforcement, intent, TestContext.Current.CancellationToken);
        var freshIntent = await store.ValidateAndConsumeAsync(grant, enforcement, CreateIntent(2), TestContext.Current.CancellationToken);
        replay.Status.ShouldBe(GrantConsumptionStatus.Reconciled);
        _ = replay.IntentReceipt.ShouldNotBeNull();
        freshIntent.Status.ShouldBe(GrantConsumptionStatus.Expired);
        freshIntent.IntentReceipt.ShouldBeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ValidateAndConsumeAsync_WhenSameIntentChangesEffectOrFence_DoesNotConsumeAnotherUse(bool changeEffect)
    {
        var store = new InMemorySecurityGrantStore(new FakeTimeProvider(_now));
        var grant = CreateGrant(2);
        var enforcement = CreateEnforcement(grant);
        var intent = CreateIntent();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        _ = await store.ValidateAndConsumeAsync(grant, enforcement, intent, TestContext.Current.CancellationToken);
        var changedEnforcement = changeEffect ? enforcement with
        {
            InputFingerprint = new InputFingerprint("sha256:changed")
        }

        : enforcement;
        var changedIntent = changeEffect ? intent : new SecurityEnforcementIntent(intent.Id, new FencingToken(9));
        var mismatch = await store.ValidateAndConsumeAsync(grant, changedEnforcement, changedIntent, TestContext.Current.CancellationToken);
        var final = await store.ValidateAndConsumeAsync(grant, enforcement, CreateIntent(2), TestContext.Current.CancellationToken);
        mismatch.Status.ShouldBe(GrantConsumptionStatus.Mismatch);
        mismatch.RemainingUses.ShouldBe(1);
        final.Status.ShouldBe(GrantConsumptionStatus.Consumed);
        final.RemainingUses.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WhenImplementationDoesNotSupportIntentReceipts_FailsBeforeLegacyConsumption()
    {
        ISecurityGrantStore store = new LegacyOnlyGrantStore();
        var grant = CreateGrant();
        var result = await store.ValidateAndConsumeAsync(grant, CreateEnforcement(grant), CreateIntent(), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GrantConsumptionStatus.Unknown);
        ((LegacyOnlyGrantStore) store).LegacyConsumptionCalls.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WhenCallerAlreadyCancelled_PreservesOriginalCancellationAndUse()
    {
        var store = new InMemorySecurityGrantStore(new FakeTimeProvider(_now));
        var grant = CreateGrant();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        using var source = new CancellationTokenSource();
        source.Cancel();
        var exception = await Should.ThrowAsync<OperationCanceledException>(async () => await store.ValidateAndConsumeAsync(grant, CreateEnforcement(grant), CreateIntent(), source.Token));
        var later = await store.ValidateAndConsumeAsync(grant, CreateEnforcement(grant), CreateIntent(2), TestContext.Current.CancellationToken);
        exception.CancellationToken.ShouldBe(source.Token);
        later.Status.ShouldBe(GrantConsumptionStatus.Consumed);
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WhenClockCancelsCaller_RollsBackUseAndIntentReceipt()
    {
        using var source = new CancellationTokenSource();
        var store = new InMemorySecurityGrantStore(new CancellingTimeProvider(_now, source));
        var grant = CreateGrant();
        var enforcement = CreateEnforcement(grant);
        var intent = CreateIntent();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        var exception = await Should.ThrowAsync<OperationCanceledException>(async () => await store.ValidateAndConsumeAsync(grant, enforcement, intent, source.Token));
        var later = await store.ValidateAndConsumeAsync(grant, enforcement, intent, TestContext.Current.CancellationToken);
        exception.CancellationToken.ShouldBe(source.Token);
        later.Status.ShouldBe(GrantConsumptionStatus.Consumed);
        later.RemainingUses.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WhenLoggerThrows_PreservesNewConsumptionAndReplay()
    {
        var store = new InMemorySecurityGrantStore(new FakeTimeProvider(_now), new ThrowingLogger());
        var grant = CreateGrant();
        var enforcement = CreateEnforcement(grant);
        var intent = CreateIntent();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        var consumed = await store.ValidateAndConsumeAsync(grant, enforcement, intent, TestContext.Current.CancellationToken);
        var replay = await store.ValidateAndConsumeAsync(grant, enforcement, intent, TestContext.Current.CancellationToken);
        consumed.Status.ShouldBe(GrantConsumptionStatus.Consumed);
        replay.Status.ShouldBe(GrantConsumptionStatus.Reconciled);
    }

    [Theory]
    [InlineData(0, "Scope")]
    [InlineData(1, "Identity")]
    [InlineData(2, "PolicyVersion")]
    public async Task RegisterAsync_WhenCapturedGrantCopyContradictsAuthorization_ThrowsBeforeRegistration(int mutation, string parameterName)
    {
        var store = new InMemorySecurityGrantStore(new FakeTimeProvider(_now));
        var original = CreateCapturedGrant();
        var replacementScope = new SecurityAuthorizationScope(original.Scope.AgentId, original.Scope.SessionId, new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null));
        var replacementIdentity = TestSupport.TestExecutionIdentity.Create(new TenantId("other-tenant"), new PrincipalId("other-principal"), ExecutionSubjectKind.Human);
        var registrationReached = false;
        Action registration = mutation switch
        {
            0 => () =>
            {
                var contradictory = original with
                {
                    Scope = replacementScope
                };
                ObserveRegistration(contradictory);
            }
            ,
            1 => () =>
            {
                var contradictory = original with
                {
                    Identity = replacementIdentity
                };
                ObserveRegistration(contradictory);
            }
            ,
            _ => () =>
            {
                var contradictory = original with
                {
                    PolicyVersion = new SecurityPolicyVersion(2)
                };
                ObserveRegistration(contradictory);
            }
            ,
        };
        var exception = Should.Throw<ArgumentException>(registration);
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe(parameterName);
        registrationReached.ShouldBeFalse();
        await store.RegisterAsync(original, TestContext.Current.CancellationToken);
        var result = await store.ValidateAndConsumeAsync(original, CreateCapturedEnforcement(original), CreateIntent(), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GrantConsumptionStatus.Consumed);
        void ObserveRegistration(SecurityGrant _) => registrationReached = true;
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WhenClockFails_ObservesFaultAndPreservesOriginalExceptionAndUse()
    {
        var clock = new SwitchableThrowingTimeProvider(_now);
        var logger = new RecordingSecurityGrantStoreLogger();
        var store = new InMemorySecurityGrantStore(clock, logger);
        var grant = CreateGrant();
        var enforcement = CreateEnforcement(grant);
        var intent = CreateIntent();
        Activity? stopped = null;
        var outcomes = new List<string>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.SecurityGrantConsume && activity.GetTagItem(AgentKitTagNames.SecurityRequestId)?.ToString() == grant.RequestId.ToString())
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(activityListener);
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name == AgentKitMetricNames.SecurityGrantConsumptionCount)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) => outcomes.Add(OutcomeFrom(tags)));
        meterListener.Start();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await store.ValidateAndConsumeAsync(grant, enforcement, intent, TestContext.Current.CancellationToken));
        exception.ShouldBeSameAs(clock.Failure);
        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("faulted");
        outcomes.ShouldContain("faulted");
        logger.Events.ShouldContain(static item => item.EventId.Id == 5027);
        logger.Events.ShouldAllBe(item => !item.Message.Contains(grant.InputFingerprint.Value, StringComparison.Ordinal));
        clock.ThrowOnRead = false;
        var later = await store.ValidateAndConsumeAsync(grant, enforcement, intent, TestContext.Current.CancellationToken);
        later.Status.ShouldBe(GrantConsumptionStatus.Consumed);
        later.RemainingUses.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WhenActivityListenerThrows_PreservesConsumption()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStarted = static activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.SecurityGrantConsume)
                {
                    throw new InvalidOperationException("listener start failure");
                }
            },
            ActivityStopped = static activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.SecurityGrantConsume)
                {
                    throw new InvalidOperationException("listener stop failure");
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var store = new InMemorySecurityGrantStore(new FakeTimeProvider(_now));
        var grant = CreateGrant();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        var result = await store.ValidateAndConsumeAsync(grant, CreateEnforcement(grant), CreateIntent(), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GrantConsumptionStatus.Consumed);
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WhenConsumed_EmitsSafeActivityAndBoundedMetric()
    {
        Activity? stopped = null;
        var outcomes = new List<string>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.SecurityGrantConsume)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(activityListener);
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name == AgentKitMetricNames.SecurityGrantConsumptionCount)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) => outcomes.Add(OutcomeFrom(tags)));
        meterListener.Start();
        var store = new InMemorySecurityGrantStore(new FakeTimeProvider(_now));
        var grant = CreateGrant();
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        var result = await store.ValidateAndConsumeAsync(grant, CreateEnforcement(grant), CreateIntent(), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(GrantConsumptionStatus.Consumed);
        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("consumed");
        activity.TagObjects.Select(static tag => tag.Value).ShouldNotContain(grant.InputFingerprint.Value);
        outcomes.ShouldBe(["consumed"]);
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

    /// <summary>Releases a fixed worker set together so every call contends for the grant store's synchronization boundary.</summary>
    /// <param name = "store">The store whose atomic consumption behavior is under test.</param>
    /// <param name = "grant">The grant evidence each worker presents.</param>
    /// <param name = "enforcement">The exact enforcement evidence each worker presents.</param>
    /// <returns>The terminal result for every concurrently released worker.</returns>
    private static async Task<GrantConsumptionResult[]> ConsumeConcurrentlyAsync(InMemorySecurityGrantStore store, SecurityGrant grant, SecurityEnforcementRequest enforcement)
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
            return await store.ValidateAndConsumeAsync(grant, enforcement, cancellationToken);
        }));
        return await Task.WhenAll(workers);
    }

    private static SecurityGrant CreateGrant(int allowedUses = 1)
    {
        var scope = new SecurityAuthorizationScope(new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000002")), new InRunOperationCorrelation(new OperationId(Guid.Parse("30000000-0000-0000-0000-000000000003")), new RunId(Guid.Parse("40000000-0000-0000-0000-000000000004")), null));
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        return new SecurityGrant(new GrantId(Guid.Parse("50000000-0000-0000-0000-000000000005")), new SecurityRequestId(Guid.Parse("60000000-0000-0000-0000-000000000006")), scope, identity, new ComponentId("filesystem"), SecurityOperationKind.FileRead, SecurityEffect.Observe, [new ProtectedResource(ProtectedResourceKind.File, "/workspace/file.txt")], new InputFingerprint("sha256:abc"), new SecurityPolicyVersion(1), new SecurityRevocationVersion(1), _now.AddMinutes(-1), _now.AddMinutes(10), allowedUses);
    }

    private static SecurityEnforcementRequest CreateEnforcement(SecurityGrant grant) => new(grant.Scope, grant.Identity, grant.Audience, grant.Kind, grant.Effect, grant.Resources, grant.InputFingerprint, grant.RevocationVersion);
    private static SecurityGrant CreateCapturedGrant()
    {
        var grant = CreateGrant();
        var authorization = new SecurityAuthorizationContext(new SecurityProfileKey("default"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.NewGuid()), grant.PolicyVersion, new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(1), new ConfigurationVersion(1), grant.Scope, grant.Identity);
        return new SecurityGrant(grant.Id, grant.RequestId, grant.Scope, grant.Identity, authorization, grant.Audience, grant.Kind, grant.Effect, grant.Resources, grant.InputFingerprint, grant.PolicyVersion, grant.RevocationVersion, grant.NotBefore, grant.ExpiresAt, grant.AllowedUses);
    }

    private static SecurityEnforcementRequest CreateCapturedEnforcement(SecurityGrant grant) => new(grant.Scope, grant.Identity, grant.Authorization.ShouldNotBeNull(), grant.Audience, grant.Kind, grant.Effect, grant.Resources, grant.InputFingerprint, grant.RevocationVersion);
    private static SecurityEnforcementIntent CreateIntent(int discriminator = 1) => new(new SecurityEnforcementIntentId(Guid.Parse($"70000000-0000-0000-0000-{discriminator:D12}")), null);
    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
    private static string OutcomeFrom(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == AgentKitTagNames.Outcome)
            {
                return tag.Value?.ToString() ?? string.Empty;
            }
        }

        throw new InvalidOperationException("Grant-consumption metric omitted its bounded outcome.");
    }

    private sealed class CancellingTimeProvider(DateTimeOffset utcNow, CancellationTokenSource cancellationSource): TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            cancellationSource.Cancel();
            return utcNow;
        }
    }

    private sealed class ThrowingLogger: ILogger<InMemorySecurityGrantStore>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => throw new InvalidOperationException("logger failure");
    }

    private sealed class LegacyOnlyGrantStore: ISecurityGrantStore
    {
        public int LegacyConsumptionCalls { get; private set; }

        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(SecurityGrant grant, SecurityEnforcementRequest enforcement, CancellationToken cancellationToken = default)
        {
            LegacyConsumptionCalls++;
            return ValueTask.FromResult(new GrantConsumptionResult(GrantConsumptionStatus.Consumed, 0, "Legacy consumption was invoked."));
        }

        public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
    }

    /// <summary>Creates isolated composition for each inherited contract case.</summary>
    /// <returns>The fixture that resolves the first-party store through dependency injection.</returns>
    protected override InMemorySecurityGrantStoreConformanceFixture CreateFixture() => new();
}
