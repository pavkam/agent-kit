// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory.Tests;

using static ArtifactTestSupport;

/// <summary>Verifies the shared in-memory projection's indexes, payload accounting, and ordering.</summary>
public sealed class ArtifactStoreStateTests
{
    private static readonly byte[] _content = "state"u8.ToArray();

    [Fact]
    public void Commit_WhenEntriesAreApplied_IndexesThemByPreparationReplayKeyAndPublishedVersion()
    {
        var state = new ArtifactStoreState();
        var prepared = Plan(state, 1, 1, "key-1");
        state.Commit(prepared.Upserts);
        var finalized = ArtifactPlanner.PlanFinalize(state, Publish(1), Now);
        state.Commit(finalized.Upserts);

        state.ByPreparation(Identity.TenantId, Preparation(1)).ShouldNotBeNull().State.ShouldBe(ArtifactEntryState.Finalized);
        state.ByReplay(Identity.TenantId, new IdempotencyKey("key-1")).ShouldNotBeNull().PreparationId.ShouldBe(Preparation(1));
        state.ByArtifact(Identity.TenantId, Artifact(1), new ArtifactVersion("1")).ShouldNotBeNull().IsDeleted.ShouldBeFalse();
        state.ByPreparation(Other.TenantId, Preparation(1)).ShouldBeNull();
        state.ByReplay(Other.TenantId, new IdempotencyKey("key-1")).ShouldBeNull();
        state.ByArtifact(Other.TenantId, Artifact(1), new ArtifactVersion("1")).ShouldBeNull();
        state.Count.ShouldBe(1);
    }

    [Fact]
    public void ByArtifact_WhenOnlyStagedOrAborted_DoesNotClaimTheVersion()
    {
        var state = new ArtifactStoreState();
        state.Commit(Plan(state, 1, 1, "key-1").Upserts);

        state.ByArtifact(Identity.TenantId, Artifact(1), new ArtifactVersion("1")).ShouldBeNull();
    }

    [Fact]
    public void HasLivePayload_WhenEntriesShareBytesInATenant_CountsPreparedAndLiveCommittedOnly()
    {
        var state = new ArtifactStoreState();
        var hash = FileSecurityBinding.ContentFingerprint(_content);
        state.Commit(Plan(state, 1, 1, "key-1").Upserts);
        state.Commit(Plan(state, 2, 2, "key-2").Upserts);
        state.HasLivePayload(Identity.TenantId, hash).ShouldBeTrue();
        state.HasLivePayload(Other.TenantId, hash).ShouldBeFalse();

        state.Commit(ArtifactPlanner.PlanAbort(state, Abort(1)).Upserts);
        state.HasLivePayload(Identity.TenantId, hash).ShouldBeTrue();
        state.Commit(ArtifactPlanner.PlanAbort(state, Abort(2)).Upserts);

        state.HasLivePayload(Identity.TenantId, hash).ShouldBeFalse();
    }

    [Fact]
    public void Commit_WhenACommittedVersionIsDeleted_StopsCountingItsPayloadButKeepsTheTombstone()
    {
        var state = new ArtifactStoreState();
        state.Commit(Plan(state, 1, 1, "key-1").Upserts);
        var reference = ((ArtifactStoreFinalized) ArtifactPlanner.PlanFinalize(state, Publish(1), Now).Result).Reference;
        state.Commit(ArtifactPlanner.PlanFinalize(state, Publish(1), Now).Upserts);

        state.Commit(ArtifactPlanner.PlanDelete(state, Delete(reference), Now).Upserts);

        state.HasLivePayload(Identity.TenantId, reference.Integrity.ContentHash).ShouldBeFalse();
        state.ByArtifact(Identity.TenantId, reference.Id, reference.Version).ShouldNotBeNull().IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public void Restore_WhenReplayingEntries_ReproducesTheAcknowledgedState()
    {
        var source = new ArtifactStoreState();
        source.Commit(Plan(source, 1, 1, "key-1").Upserts);
        source.Commit(Plan(source, 2, 2, "key-2").Upserts);
        source.Commit(ArtifactPlanner.PlanFinalize(source, Publish(2), Now).Upserts);

        var restored = new ArtifactStoreState();
        foreach (var entry in source.Snapshot())
        {
            restored.Restore(entry);
        }

        restored.Snapshot().ShouldBe(source.Snapshot());
        restored.HasLivePayload(Identity.TenantId, FileSecurityBinding.ContentFingerprint(_content)).ShouldBeTrue();
    }

    [Fact]
    public void Snapshot_WhenEntriesSpanTenants_OrdersByTenantThenPreparation()
    {
        var state = new ArtifactStoreState();
        state.Commit(Plan(state, 3, 3, "c").Upserts);
        state.Commit(Plan(state, 1, 1, "a", Other).Upserts);
        state.Commit(Plan(state, 2, 2, "b").Upserts);

        state.Snapshot().Select(static entry => (entry.TenantId.Value, entry.PreparationId)).ShouldBe(
            [("other", Preparation(1)), ("tenant", Preparation(2)), ("tenant", Preparation(3))]);
    }

    private static ArtifactPlan<ArtifactStorePrepareResult> Plan(ArtifactStoreState state, int preparation, int artifact, string key, ExecutionIdentity? identity = null) =>
        ArtifactPlanner.PlanPrepare(state, Prepare(_content, preparation, artifact, key, identity), Now);
}
