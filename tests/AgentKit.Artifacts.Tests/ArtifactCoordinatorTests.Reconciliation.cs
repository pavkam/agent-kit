// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

public sealed partial class ArtifactCoordinatorTests
{
    private static readonly ArtifactReferenceCommitIntentId _intentId = new(Guid.Parse("a0000000-0000-0000-0000-0000000000a1"));

    [Fact]
    public async Task ReconcileAsync_WhenNoIntentStoreIsComposed_RetainsConservativelyAsEvidenceUnavailable()
    {
        using var harness = CoordinatorHarness.Create();

        var result = await harness.Coordinator.ReconcileAsync(ArtifactTestData.Reconcile(new ArtifactPreparationId(Guid.Parse("b0000000-0000-0000-0000-0000000000b1"))), TestContext.Current.CancellationToken);

        result.ShouldBe(new ArtifactReconciliationPending(ArtifactReconciliationPendingReason.EvidenceUnavailable));
    }

    [Fact]
    public async Task ReconcileAsync_WhenNoIntentWasRecorded_RetainsAndDeletesNothing()
    {
        using var harness = CoordinatorHarness.Create(withIntents: true);
        var reference = await harness.CommitAsync(_content);
        harness.Clock.Advance(TimeSpan.FromDays(3));

        var result = await harness.Coordinator.ReconcileAsync(ArtifactTestData.Reconcile(new ArtifactPreparationId(Guid.Parse("b0000000-0000-0000-0000-0000000000b1"))), TestContext.Current.CancellationToken);

        result.ShouldBe(new ArtifactReconciliationPending(ArtifactReconciliationPendingReason.EvidenceUnavailable));
        (await harness.ReadTextAsync(reference)).ShouldBe("complete output");
    }

    [Fact]
    public async Task ReconcileAsync_WhenTheReferenceWasCommitted_ReportsCommittedAndKeepsTheContent()
    {
        using var harness = CoordinatorHarness.Create(withIntents: true);
        var (preparation, reference) = await PrepareAndFinalizeAsync(harness);
        _ = await harness.Intents!.TransitionAsync(
            ArtifactTestData.Identity.TenantId, preparation, ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Committed,
            harness.Clock.GetUtcNow(), TestContext.Current.CancellationToken);
        harness.Clock.Advance(TimeSpan.FromDays(3));

        var result = await harness.Coordinator.ReconcileAsync(ArtifactTestData.Reconcile(preparation), TestContext.Current.CancellationToken);

        result.ShouldBe(new ArtifactReconciled(ArtifactReconciliationDisposition.ReferenceCommitted));
        (await harness.ReadTextAsync(reference)).ShouldBe("complete output");
        harness.Sink.Events.OfType<ArtifactReconciledEvent>().ShouldHaveSingleItem().Disposition.ShouldBe(ArtifactReconciliationDisposition.ReferenceCommitted);
    }

    [Fact]
    public async Task ReconcileAsync_WhenTheRetentionWindowIsStillOpen_RetainsWithoutFencing()
    {
        using var harness = CoordinatorHarness.Create(withIntents: true);
        var (preparation, reference) = await PrepareAndFinalizeAsync(harness);
        harness.Clock.Advance(TimeSpan.FromHours(23));

        var result = await harness.Coordinator.ReconcileAsync(ArtifactTestData.Reconcile(preparation), TestContext.Current.CancellationToken);

        result.ShouldBe(new ArtifactReconciliationPending(ArtifactReconciliationPendingReason.RetentionWindowOpen));
        (await StateAsync(harness, preparation)).ShouldBe(ArtifactReferenceCommitState.Pending);
        (await harness.ReadTextAsync(reference)).ShouldBe("complete output");
    }

    [Fact]
    public async Task ReconcileAsync_WhenThePinOutlastsOrphanRetention_HonoursThePin()
    {
        using var harness = CoordinatorHarness.Create(withIntents: true);
        var (preparation, _) = await PrepareAsync(harness, pinFor: TimeSpan.FromDays(10));
        harness.Clock.Advance(TimeSpan.FromDays(5));

        var result = await harness.Coordinator.ReconcileAsync(ArtifactTestData.Reconcile(preparation), TestContext.Current.CancellationToken);

        result.ShouldBe(new ArtifactReconciliationPending(ArtifactReconciliationPendingReason.RetentionWindowOpen));
    }

    [Fact]
    public async Task ReconcileAsync_WhenTheWindowClosedAndOnlyStagingExists_FencesThenAbortsAndCollects()
    {
        using var harness = CoordinatorHarness.Create(withIntents: true);
        var (preparation, _) = await PrepareAsync(harness);
        harness.Clock.Advance(TimeSpan.FromDays(2));

        var result = await harness.Coordinator.ReconcileAsync(ArtifactTestData.Reconcile(preparation), TestContext.Current.CancellationToken);
        var finalize = await harness.Coordinator.FinalizeAsync(ArtifactTestData.Finalize(preparation, key: "late"), TestContext.Current.CancellationToken);
        var replay = await harness.Coordinator.ReconcileAsync(ArtifactTestData.Reconcile(preparation, key: "reconcile-2"), TestContext.Current.CancellationToken);

        result.ShouldBe(new ArtifactReconciled(ArtifactReconciliationDisposition.Collected));
        finalize.ShouldBeOfType<ArtifactFinalizeRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        replay.ShouldBe(new ArtifactReconciled(ArtifactReconciliationDisposition.AlreadyCollected));
        (await StateAsync(harness, preparation)).ShouldBe(ArtifactReferenceCommitState.Collected);
    }

    [Fact]
    public async Task ReconcileAsync_WhenContentFinalizedButNeverReferenced_DeletesItThroughTheOrdinaryPath()
    {
        using var harness = CoordinatorHarness.Create(withIntents: true);
        var (preparation, reference) = await PrepareAndFinalizeAsync(harness);
        harness.Clock.Advance(TimeSpan.FromDays(2));

        var result = await harness.Coordinator.ReconcileAsync(ArtifactTestData.Reconcile(preparation), TestContext.Current.CancellationToken);
        var read = await harness.Coordinator.ReadAsync(ArtifactTestData.Read(reference), TestContext.Current.CancellationToken);

        result.ShouldBe(new ArtifactReconciled(ArtifactReconciliationDisposition.Collected));
        read.ShouldBeOfType<ArtifactReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        (await StateAsync(harness, preparation)).ShouldBe(ArtifactReferenceCommitState.Collected);
    }

    [Fact]
    public async Task ReconcileAsync_WhenLegalHoldProtectsFinalizedContent_RetainsAndRefusesALateCommit()
    {
        using var harness = CoordinatorHarness.Create(withIntents: true);
        var held = ArtifactTestData.Metadata(_content, retention: new ArtifactRetention(new ArtifactRetentionPolicyKey("hold"), null, true));
        var (preparation, reference) = await PrepareAndFinalizeAsync(harness, held);
        harness.Clock.Advance(TimeSpan.FromDays(2));

        var result = await harness.Coordinator.ReconcileAsync(ArtifactTestData.Reconcile(preparation), TestContext.Current.CancellationToken);
        var lateCommit = await harness.Intents!.TransitionAsync(
            ArtifactTestData.Identity.TenantId, preparation, ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Committed,
            harness.Clock.GetUtcNow(), TestContext.Current.CancellationToken);

        result.ShouldBe(new ArtifactReconciliationPending(ArtifactReconciliationPendingReason.RetentionHold));
        lateCommit.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.StateChanged);
        (await StateAsync(harness, preparation)).ShouldBe(ArtifactReferenceCommitState.Fenced);
        (await harness.ReadTextAsync(reference)).ShouldBe("complete output");
    }

    [Fact]
    public async Task ReconcileAsync_WhenALateCommitWinsTheFenceRace_TheCommitIsNeverCollected()
    {
        var racing = new RacingIntentStore();
        using var harness = CoordinatorHarness.Create(extra: services => _ = services.AddSingleton<IArtifactReferenceCommitIntentStore>(racing));
        var (preparation, reference) = await PrepareAndFinalizeAsync(harness);
        harness.Clock.Advance(TimeSpan.FromDays(2));

        var result = await harness.Coordinator.ReconcileAsync(ArtifactTestData.Reconcile(preparation), TestContext.Current.CancellationToken);

        racing.CommittedBeforeFence.ShouldBeTrue();
        result.ShouldBe(new ArtifactReconciled(ArtifactReconciliationDisposition.ReferenceCommitted));
        (await StateAsync(harness, preparation)).ShouldBe(ArtifactReferenceCommitState.Committed);
        (await harness.ReadTextAsync(reference)).ShouldBe("complete output");
    }

    [Fact]
    public async Task ReconcileAsync_WhenAFencedIntentResumesAfterACrash_CompletesCollection()
    {
        using var harness = CoordinatorHarness.Create(withIntents: true);
        var (preparation, _) = await PrepareAsync(harness);
        harness.Clock.Advance(TimeSpan.FromDays(2));
        _ = await harness.Intents!.TransitionAsync(
            ArtifactTestData.Identity.TenantId, preparation, ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced,
            harness.Clock.GetUtcNow(), TestContext.Current.CancellationToken);

        var result = await harness.Coordinator.ReconcileAsync(ArtifactTestData.Reconcile(preparation), TestContext.Current.CancellationToken);

        result.ShouldBe(new ArtifactReconciled(ArtifactReconciliationDisposition.Collected));
        (await StateAsync(harness, preparation)).ShouldBe(ArtifactReferenceCommitState.Collected);
    }

    [Fact]
    public async Task ReconcileAsync_WhenAuthorityDeniesCollection_ReportsDeniedAndKeepsTheObject()
    {
        using var harness = CoordinatorHarness.Create(withIntents: true);
        var (preparation, _) = await PrepareAsync(harness);
        harness.Clock.Advance(TimeSpan.FromDays(2));
        harness.Authority.Deny = true;

        var result = await harness.Coordinator.ReconcileAsync(ArtifactTestData.Reconcile(preparation), TestContext.Current.CancellationToken);
        harness.Authority.Deny = false;
        var stillStaged = await harness.Coordinator.AbortAsync(ArtifactTestData.Abort(preparation), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactReconciliationRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        stillStaged.ShouldBe(new ArtifactAborted(false));
        (await StateAsync(harness, preparation)).ShouldBe(ArtifactReferenceCommitState.Fenced);
    }

    [Fact]
    public async Task ReconcileAsync_WhenNothingWasEverStagedAndEveryBackendAnswers_CollectsTheEmptyIntent()
    {
        using var harness = CoordinatorHarness.Create(withIntents: true);
        var preparation = new ArtifactPreparationId(Guid.Parse("b0000000-0000-0000-0000-0000000000b2"));
        await RecordIntentAsync(harness, preparation, TimeSpan.FromHours(1));
        harness.Clock.Advance(TimeSpan.FromDays(2));

        var result = await harness.Coordinator.ReconcileAsync(ArtifactTestData.Reconcile(preparation), TestContext.Current.CancellationToken);

        result.ShouldBe(new ArtifactReconciled(ArtifactReconciliationDisposition.Collected));
    }

    [Fact]
    public async Task ReconcileAsync_WhenABackendCannotAnswer_RetainsRatherThanAssumingTheObjectIsGone()
    {
        using var harness = CoordinatorHarness.Create(
            withIntents: true,
            configureProfile: profile => profile.Routes[new ArtifactDirectoryId("ghost-dir")] = new ArtifactBackendKey("ghost"));
        var preparation = new ArtifactPreparationId(Guid.Parse("b0000000-0000-0000-0000-0000000000b3"));
        await RecordIntentAsync(harness, preparation, TimeSpan.FromHours(1));
        harness.Clock.Advance(TimeSpan.FromDays(2));

        var result = await harness.Coordinator.ReconcileAsync(ArtifactTestData.Reconcile(preparation), TestContext.Current.CancellationToken);

        result.ShouldBe(new ArtifactReconciliationPending(ArtifactReconciliationPendingReason.EvidenceUnavailable));
        (await StateAsync(harness, preparation)).ShouldBe(ArtifactReferenceCommitState.Fenced);
    }

    [Fact]
    public async Task ReconcileAsync_WhenAnotherTenantReconciles_CannotSeeOrCollectTheOwnersIntent()
    {
        using var harness = CoordinatorHarness.Create(withIntents: true);
        var (preparation, reference) = await PrepareAndFinalizeAsync(harness);
        harness.Clock.Advance(TimeSpan.FromDays(2));

        var result = await harness.Coordinator.ReconcileAsync(ArtifactTestData.Reconcile(preparation, ArtifactTestData.OtherAuthorization), TestContext.Current.CancellationToken);

        result.ShouldBe(new ArtifactReconciliationPending(ArtifactReconciliationPendingReason.EvidenceUnavailable));
        (await harness.ReadTextAsync(reference)).ShouldBe("complete output");
        (await StateAsync(harness, preparation)).ShouldBe(ArtifactReferenceCommitState.Pending);
    }

    private static async Task<(ArtifactPreparationId Preparation, ArtifactReference Reference)> PrepareAndFinalizeAsync(CoordinatorHarness harness, ArtifactMetadata? metadata = null)
    {
        var (preparation, _) = await PrepareAsync(harness, metadata: metadata);
        var reference = (await harness.Coordinator.FinalizeAsync(ArtifactTestData.Finalize(preparation), TestContext.Current.CancellationToken))
            .ShouldBeOfType<ArtifactFinalized>().Reference;
        return (preparation, reference);
    }

    private static async Task<(ArtifactPreparationId Preparation, ArtifactPrepared Prepared)> PrepareAsync(
        CoordinatorHarness harness, ArtifactMetadata? metadata = null, TimeSpan? pinFor = null)
    {
        var prepared = (await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content, metadata), TestContext.Current.CancellationToken))
            .ShouldBeOfType<ArtifactPrepared>();
        await RecordIntentAsync(harness, prepared.PreparationId, pinFor ?? TimeSpan.FromHours(1), prepared.ArtifactId, prepared.Version);
        return (prepared.PreparationId, prepared);
    }

    private static async Task RecordIntentAsync(
        CoordinatorHarness harness, ArtifactPreparationId preparation, TimeSpan pinFor, ArtifactId? artifact = null, ArtifactVersion? version = null)
    {
        var now = harness.Clock.GetUtcNow();
        var recorded = await harness.Intents!.RecordAsync(
            new ArtifactReferenceCommitIntent(
                _intentId, ArtifactTestData.Identity.TenantId, preparation, artifact ?? new ArtifactId(Guid.Parse("c0000000-0000-0000-0000-0000000000c1")),
                version ?? new ArtifactVersion("1"), new ArtifactOwnerId("session:owner"), new ArtifactPin(_intentId, now, now + pinFor),
                ArtifactReferenceCommitState.Pending, now, now),
            TestContext.Current.CancellationToken);
        recorded.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.Applied);
    }

    private static async Task<ArtifactReferenceCommitState> StateAsync(CoordinatorHarness harness, ArtifactPreparationId preparation) =>
        (await harness.Intents!.GetAsync(ArtifactTestData.Identity.TenantId, preparation, TestContext.Current.CancellationToken)).Intent!.State;

    /// <summary>Commits the reference the instant the reconciler tries to fence it, so the conditional transition has exactly one winner.</summary>
    private sealed class RacingIntentStore: IArtifactReferenceCommitIntentStore
    {
        private readonly InMemoryArtifactReferenceCommitIntentStore _inner = new();

        internal bool CommittedBeforeFence { get; private set; }

        public ValueTask<ArtifactReferenceCommitIntentResult> RecordAsync(ArtifactReferenceCommitIntent intent, CancellationToken cancellationToken = default) =>
            _inner.RecordAsync(intent, cancellationToken);

        public ValueTask<ArtifactReferenceCommitIntentResult> GetAsync(TenantId tenantId, ArtifactPreparationId preparationId, CancellationToken cancellationToken = default) =>
            _inner.GetAsync(tenantId, preparationId, cancellationToken);

        public async ValueTask<ArtifactReferenceCommitIntentResult> TransitionAsync(
            TenantId tenantId, ArtifactPreparationId preparationId, ArtifactReferenceCommitState expected,
            ArtifactReferenceCommitState nextState, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            if (nextState == ArtifactReferenceCommitState.Fenced && !CommittedBeforeFence)
            {
                CommittedBeforeFence = true;
                _ = await _inner.TransitionAsync(tenantId, preparationId, ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Committed, at, cancellationToken);
            }

            return await _inner.TransitionAsync(tenantId, preparationId, expected, nextState, at, cancellationToken);
        }

        public ValueTask<ImmutableArray<ArtifactReferenceCommitIntent>> ListPendingAsync(TenantId tenantId, DateTimeOffset recordedBefore, int limit, CancellationToken cancellationToken = default) =>
            _inner.ListPendingAsync(tenantId, recordedBefore, limit, cancellationToken);
    }
}
