// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory.Tests;



/// <summary>Verifies InMemoryArtifactStore behavior and contracts.</summary>
public sealed class InMemoryArtifactStoreTests: ArtifactStoreConformanceTests<InMemoryArtifactStoreConformanceFixture>
{
    private static readonly DateTimeOffset _now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
    [Fact]
    public void Constructor_WhenIntentIdsIsNull_ThrowsWithExactParameterName()
    {
        var fixture = new StoreFixture();
        var exception = Should.Throw<ArgumentNullException>(() => new InMemoryArtifactStore(fixture.Grants, fixture.Clock, null!));
        exception.ParamName.ShouldBe("intentIds");
    }

    [Fact]
    public async Task PrepareAsync_WhenStoreReconcilesEarlierIntent_DoesNotCreateState()
    {
        var fixture = new StoreFixture();
        var grants = new IntentReceiptGrantStore
        {
            Status = GrantConsumptionStatus.Reconciled
        };
        var store = new InMemoryArtifactStore(grants, fixture.Clock);
        var request = fixture.CreatePrepare("content"u8.ToArray());
        var rejected = await store.PrepareAsync(request, TestContext.Current.CancellationToken);
        grants.Status = GrantConsumptionStatus.Consumed;
        var accepted = await store.PrepareAsync(request, TestContext.Current.CancellationToken);
        rejected.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        _ = accepted.ShouldBeOfType<ArtifactPrepared>();
    }

    [Fact]
    public async Task PrepareAsync_WhenConsumedReceiptIsMissing_DoesNotCreateState()
    {
        var fixture = new StoreFixture();
        var grants = new IntentReceiptGrantStore
        {
            IncludeReceipt = false
        };
        var store = new InMemoryArtifactStore(grants, fixture.Clock);
        var request = fixture.CreatePrepare("content"u8.ToArray());
        var rejected = await store.PrepareAsync(request, TestContext.Current.CancellationToken);
        grants.IncludeReceipt = true;
        var accepted = await store.PrepareAsync(request, TestContext.Current.CancellationToken);
        rejected.ShouldBeOfType<ArtifactPrepareRejected>().Failure.SafeMessage.ShouldContain("enforcement-intent receipt");
        _ = accepted.ShouldBeOfType<ArtifactPrepared>();
    }

    [Fact]
    public async Task PrepareAsync_WhenReceiptReferencesAnotherIntent_DoesNotCreateState()
    {
        var fixture = new StoreFixture();
        var grants = new IntentReceiptGrantStore
        {
            ReturnExactReceipt = false
        };
        var store = new InMemoryArtifactStore(grants, fixture.Clock);
        var request = fixture.CreatePrepare("content"u8.ToArray());
        var rejected = await store.PrepareAsync(request, TestContext.Current.CancellationToken);
        grants.ReturnExactReceipt = true;
        var accepted = await store.PrepareAsync(request, TestContext.Current.CancellationToken);
        rejected.ShouldBeOfType<ArtifactPrepareRejected>().Failure.SafeMessage.ShouldContain("enforcement-intent receipt");
        _ = accepted.ShouldBeOfType<ArtifactPrepared>();
    }

    [Fact]
    public async Task PrepareAsync_WhenCallerCancelsDuringNonCooperativeConsumption_DoesNotCreateState()
    {
        var fixture = new StoreFixture();
        using var cancellation = new CancellationTokenSource();
        var grants = new IntentReceiptGrantStore
        {
            OnConsumption = cancellation.Cancel
        };
        var store = new InMemoryArtifactStore(grants, fixture.Clock);
        var request = fixture.CreatePrepare("content"u8.ToArray());
        var action = async () => await store.PrepareAsync(request, cancellation.Token);
        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        grants.OnConsumption = null;
        var accepted = await store.PrepareAsync(request, TestContext.Current.CancellationToken);
        _ = accepted.ShouldBeOfType<ArtifactPrepared>();
    }

    [Fact]
    public async Task PrepareAsync_WhenCapturedGrantIsRegistered_ConsumesItsExactAuthorizationEvidence()
    {
        var fixture = new StoreFixture();
        var request = fixture.CreatePrepare("captured"u8.ToArray(), capturedAuthorization: true);
        await fixture.RegisterPrepareGrantAsync(request);
        var result = await fixture.Store.PrepareAsync(request, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<ArtifactPrepared>();
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("identity")]
    public async Task PrepareAsync_WhenCapturedAuthorizationDoesNotMatch_DeniesBeforeGrantConsumptionOrState(string mismatch)
    {
        var fixture = new StoreFixture();
        var grants = new IntentReceiptGrantStore();
        var store = new InMemoryArtifactStore(grants, fixture.Clock);
        var request = fixture.CreatePrepare("content"u8.ToArray(), capturedAuthorization: true);
        var mismatched = mismatch == "scope" ? WithScope(request, fixture.CreateScope()) : WithIdentity(request, StoreFixture.CreateIdentity("other-tenant", "other-principal"));
        var rejected = await store.PrepareAsync(mismatched, TestContext.Current.CancellationToken);
        var accepted = await store.PrepareAsync(request, TestContext.Current.CancellationToken);
        rejected.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        grants.ConsumptionCount.ShouldBe(1);
        _ = accepted.ShouldBeOfType<ArtifactPrepared>();
    }

    [Theory]
    [InlineData("version")]
    [InlineData("profile-key")]
    [InlineData("profile-version")]
    [InlineData("created-at")]
    [InlineData("expires-at")]
    public async Task PrepareAsync_WhenBoundAttemptFieldChanges_CannotUseExistingGrant(string field)
    {
        var fixture = new StoreFixture();
        var authorized = fixture.CreatePrepare("content"u8.ToArray());
        var changed = ChangeAttemptField(authorized, field);
        await fixture.RegisterPrepareGrantAsync(changed);
        var rejected = await fixture.Store.PrepareAsync(changed, TestContext.Current.CancellationToken);
        var valid = fixture.CreatePrepare("content"u8.ToArray(), artifactId: authorized.ArtifactId, version: authorized.Version, preparationId: authorized.PreparationId, createdAt: authorized.CreatedAt, expiresAt: authorized.ExpiresAt);
        await fixture.RegisterPrepareGrantAsync(valid);
        var accepted = await fixture.Store.PrepareAsync(valid, TestContext.Current.CancellationToken);
        rejected.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        _ = accepted.ShouldBeOfType<ArtifactPrepared>();
    }

    [Fact]
    public async Task FinalizeAsync_WhenTenantDiffers_DoesNotRemoveOwnersPreparation()
    {
        var fixture = new StoreFixture();
        var prepare = fixture.CreatePrepare("output"u8.ToArray());
        await fixture.RegisterPrepareGrantAsync(prepare);
        _ = await fixture.Store.PrepareAsync(prepare, TestContext.Current.CancellationToken);
        var otherIdentity = TestSupport.TestExecutionIdentity.Create(new TenantId("other"), new PrincipalId("other-principal"), ExecutionSubjectKind.Human);
        var foreignFinalize = fixture.CreateFinalize(prepare.PreparationId, otherIdentity);
        await fixture.RegisterFinalizeGrantAsync(foreignFinalize);
        var foreign = await fixture.Store.FinalizeAsync(foreignFinalize, TestContext.Current.CancellationToken);
        var ownerFinalize = fixture.CreateFinalize(prepare.PreparationId, prepare.Identity);
        await fixture.RegisterFinalizeGrantAsync(ownerFinalize);
        var owner = await fixture.Store.FinalizeAsync(ownerFinalize, TestContext.Current.CancellationToken);
        foreign.ShouldBeOfType<ArtifactFinalizeRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        _ = owner.ShouldBeOfType<ArtifactFinalized>();
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("creator")]
    public async Task PrepareAsync_WhenPartitionIdentityDiffersFromAuthenticatedIdentity_DeniesBeforeState(string field)
    {
        var fixture = new StoreFixture();
        var request = field == "tenant" ? fixture.CreatePrepare("content"u8.ToArray(), tenantId: new TenantId("other")) : fixture.CreatePrepare("content"u8.ToArray(), createdBy: new PrincipalId("other"));
        await fixture.RegisterPrepareGrantAsync(request);
        var rejected = await fixture.Store.PrepareAsync(request, TestContext.Current.CancellationToken);
        var valid = fixture.CreatePrepare("content"u8.ToArray(), artifactId: request.ArtifactId, preparationId: request.PreparationId);
        await fixture.RegisterPrepareGrantAsync(valid);
        var accepted = await fixture.Store.PrepareAsync(valid, TestContext.Current.CancellationToken);
        rejected.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        _ = accepted.ShouldBeOfType<ArtifactPrepared>();
    }

    [Fact]
    public async Task FinalizeAsync_WhenPreparationExpires_RemovesUnpublishedState()
    {
        var fixture = new StoreFixture();
        var prepare = fixture.CreatePrepare("output"u8.ToArray(), expiresAt: _now.AddSeconds(1));
        await fixture.RegisterPrepareGrantAsync(prepare);
        _ = await fixture.Store.PrepareAsync(prepare, TestContext.Current.CancellationToken);
        fixture.Clock.Advance(TimeSpan.FromSeconds(2));
        var finalize = fixture.CreateFinalize(prepare.PreparationId, prepare.Identity);
        await fixture.RegisterFinalizeGrantAsync(finalize);
        var first = await fixture.Store.FinalizeAsync(finalize, TestContext.Current.CancellationToken);
        var retry = fixture.CreateFinalize(prepare.PreparationId, prepare.Identity);
        await fixture.RegisterFinalizeGrantAsync(retry);
        var second = await fixture.Store.FinalizeAsync(retry, TestContext.Current.CancellationToken);
        first.ShouldBeOfType<ArtifactFinalizeRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        second.ShouldBeOfType<ArtifactFinalizeRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
    }

    [Fact]
    public async Task AbortAsync_WhenArtifactWasCommitted_RejectsWithoutDeletingCommit()
    {
        var fixture = new StoreFixture();
        var prepare = fixture.CreatePrepare("output"u8.ToArray());
        await fixture.RegisterPrepareGrantAsync(prepare);
        _ = await fixture.Store.PrepareAsync(prepare, TestContext.Current.CancellationToken);
        var finalize = fixture.CreateFinalize(prepare.PreparationId, prepare.Identity);
        await fixture.RegisterFinalizeGrantAsync(finalize);
        var committed = await fixture.Store.FinalizeAsync(finalize, TestContext.Current.CancellationToken);
        var abort = fixture.CreateAbort(prepare.PreparationId, prepare.Identity);
        await fixture.RegisterAbortGrantAsync(abort);
        var rejected = await fixture.Store.AbortAsync(abort, TestContext.Current.CancellationToken);
        var replayFinalize = fixture.CreateFinalize(prepare.PreparationId, prepare.Identity);
        await fixture.RegisterFinalizeGrantAsync(replayFinalize);
        var replay = await fixture.Store.FinalizeAsync(replayFinalize, TestContext.Current.CancellationToken);
        rejected.ShouldBeOfType<ArtifactAbortRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Conflict);
        replay.ShouldBe(committed);
    }

    [Fact]
    public async Task ReadAsync_WhenReferenceIsCommitted_ReturnsCompleteOwnedContent()
    {
        var fixture = new StoreFixture();
        var reference = await fixture.CommitAsync("complete output"u8.ToArray());
        var read = fixture.CreateRead(reference, fixture.Identity);
        await fixture.RegisterReadGrantAsync(read);
        var result = await fixture.Store.ReadAsync(read, TestContext.Current.CancellationToken);
        await using var opened = result.ShouldBeOfType<ArtifactReadOpened>();
        using var reader = new StreamReader(opened.Content);
        (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).ShouldBe("complete output");
    }

    [Fact]
    public async Task ReadAsync_WhenGrantWasBoundToDifferentDirectory_DeniesWithoutChangingContent()
    {
        var fixture = new StoreFixture();
        var reference = await fixture.CommitAsync("complete output"u8.ToArray());
        var authorized = fixture.CreateRead(reference, fixture.Identity);
        await fixture.RegisterReadGrantAsync(authorized);
        var changedReference = new ArtifactReference(reference.Id, reference.Version, new ArtifactDirectoryId("different-directory"), reference.ProfileKey, reference.ProfileVersion, reference.TenantId, reference.OwnerId, reference.CreatedBy, reference.MediaType, reference.Length, reference.Integrity, reference.Classification, reference.Ownership, reference.Mutability, reference.Retention, reference.CreatedAt);
        var changedRequest = new ArtifactStoreReadRequest(changedReference, authorized.Scope, authorized.Identity, authorized.Grant);
        var rejected = await fixture.Store.ReadAsync(changedRequest, TestContext.Current.CancellationToken);
        var validRequest = fixture.CreateRead(reference, fixture.Identity);
        await fixture.RegisterReadGrantAsync(validRequest);
        var valid = await fixture.Store.ReadAsync(validRequest, TestContext.Current.CancellationToken);
        rejected.ShouldBeOfType<ArtifactReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        await using var opened = valid.ShouldBeOfType<ArtifactReadOpened>();
        using var reader = new StreamReader(opened.Content);
        (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).ShouldBe("complete output");
    }

    [Fact]
    public async Task DeleteAsync_WhenCommitted_RemovesContentAndReplaysAsAlreadyAbsent()
    {
        var fixture = new StoreFixture();
        var reference = await fixture.CommitAsync("complete output"u8.ToArray());
        var delete = fixture.CreateDelete(reference, fixture.Identity);
        await fixture.RegisterDeleteGrantAsync(delete);
        var deleted = await fixture.Store.DeleteAsync(delete, TestContext.Current.CancellationToken);
        var retry = fixture.CreateDelete(reference, fixture.Identity);
        await fixture.RegisterDeleteGrantAsync(retry);
        var replay = await fixture.Store.DeleteAsync(retry, TestContext.Current.CancellationToken);
        var read = fixture.CreateRead(reference, fixture.Identity);
        await fixture.RegisterReadGrantAsync(read);
        var missing = await fixture.Store.ReadAsync(read, TestContext.Current.CancellationToken);
        deleted.ShouldBe(new ArtifactDeleted(false));
        replay.ShouldBe(new ArtifactDeleted(true));
        missing.ShouldBeOfType<ArtifactReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_WhenDeletedKeyBelongsToAnotherTenant_ReturnsNotFound()
    {
        var fixture = new StoreFixture();
        var reference = await fixture.CommitAsync("complete output"u8.ToArray());
        var delete = fixture.CreateDelete(reference, fixture.Identity);
        await fixture.RegisterDeleteGrantAsync(delete);
        _ = await fixture.Store.DeleteAsync(delete, TestContext.Current.CancellationToken);
        var otherIdentity = TestSupport.TestExecutionIdentity.Create(new TenantId("other"), new PrincipalId("other-principal"), ExecutionSubjectKind.Human);
        var foreignReference = new ArtifactReference(reference.Id, reference.Version, reference.DirectoryId, reference.ProfileKey, reference.ProfileVersion, otherIdentity.TenantId, new ArtifactOwnerId("other-owner"), otherIdentity.PrincipalId, reference.MediaType, reference.Length, reference.Integrity, reference.Classification, reference.Ownership, reference.Mutability, reference.Retention, reference.CreatedAt);
        var foreignDelete = fixture.CreateDelete(foreignReference, otherIdentity);
        await fixture.RegisterDeleteGrantAsync(foreignDelete);
        var result = await fixture.Store.DeleteAsync(foreignDelete, TestContext.Current.CancellationToken);
        result.ShouldBeOfType<ArtifactDeleteRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_WhenDeletedReferenceDiffers_ReturnsNotFoundAndPreservesExactReplay()
    {
        var fixture = new StoreFixture();
        var reference = await fixture.CommitAsync("complete output"u8.ToArray());
        var delete = fixture.CreateDelete(reference, fixture.Identity);
        await fixture.RegisterDeleteGrantAsync(delete);
        _ = await fixture.Store.DeleteAsync(delete, TestContext.Current.CancellationToken);
        var changedReference = new ArtifactReference(reference.Id, reference.Version, new ArtifactDirectoryId("different-directory"), reference.ProfileKey, reference.ProfileVersion, reference.TenantId, reference.OwnerId, reference.CreatedBy, reference.MediaType, reference.Length, reference.Integrity, reference.Classification, reference.Ownership, reference.Mutability, reference.Retention, reference.CreatedAt);
        var changedDelete = fixture.CreateDelete(changedReference, fixture.Identity);
        await fixture.RegisterDeleteGrantAsync(changedDelete);
        var rejected = await fixture.Store.DeleteAsync(changedDelete, TestContext.Current.CancellationToken);
        var exactReplay = fixture.CreateDelete(reference, fixture.Identity);
        await fixture.RegisterDeleteGrantAsync(exactReplay);
        var replayed = await fixture.Store.DeleteAsync(exactReplay, TestContext.Current.CancellationToken);
        rejected.ShouldBeOfType<ArtifactDeleteRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        replayed.ShouldBe(new ArtifactDeleted(true));
    }

    [Fact]
    public async Task AbortAsync_WhenFinalizedArtifactWasDeleted_HidesReceiptFromForeignTenant()
    {
        var fixture = new StoreFixture();
        var prepare = fixture.CreatePrepare("complete output"u8.ToArray());
        await fixture.RegisterPrepareGrantAsync(prepare);
        _ = await fixture.Store.PrepareAsync(prepare, TestContext.Current.CancellationToken);
        var finalize = fixture.CreateFinalize(prepare.PreparationId, prepare.Identity);
        await fixture.RegisterFinalizeGrantAsync(finalize);
        var reference = (await fixture.Store.FinalizeAsync(finalize, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactFinalized>().Reference;
        var delete = fixture.CreateDelete(reference, fixture.Identity);
        await fixture.RegisterDeleteGrantAsync(delete);
        _ = await fixture.Store.DeleteAsync(delete, TestContext.Current.CancellationToken);
        var otherIdentity = TestSupport.TestExecutionIdentity.Create(new TenantId("other"), new PrincipalId("other-principal"), ExecutionSubjectKind.Human);
        var foreignAbort = fixture.CreateAbort(prepare.PreparationId, otherIdentity);
        await fixture.RegisterAbortGrantAsync(foreignAbort);
        var foreign = await fixture.Store.AbortAsync(foreignAbort, TestContext.Current.CancellationToken);
        var ownerAbort = fixture.CreateAbort(prepare.PreparationId, prepare.Identity);
        await fixture.RegisterAbortGrantAsync(ownerAbort);
        var owner = await fixture.Store.AbortAsync(ownerAbort, TestContext.Current.CancellationToken);
        foreign.ShouldBeOfType<ArtifactAbortRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        owner.ShouldBe(new ArtifactAborted(true));
    }

    [Fact]
    public async Task FinalizeAsync_WhenImmutableVersionWasDeleted_RejectsWithoutConsumingPreparation()
    {
        var fixture = new StoreFixture();
        var reference = await fixture.CommitAsync("complete output"u8.ToArray());
        var delete = fixture.CreateDelete(reference, fixture.Identity);
        await fixture.RegisterDeleteGrantAsync(delete);
        _ = await fixture.Store.DeleteAsync(delete, TestContext.Current.CancellationToken);
        var replacement = fixture.CreatePrepare("replacement"u8.ToArray(), idempotencyKey: "replacement", artifactId: reference.Id, version: reference.Version);
        await fixture.RegisterPrepareGrantAsync(replacement);
        _ = await fixture.Store.PrepareAsync(replacement, TestContext.Current.CancellationToken);
        var finalize = fixture.CreateFinalize(replacement.PreparationId, replacement.Identity);
        await fixture.RegisterFinalizeGrantAsync(finalize);
        var rejected = await fixture.Store.FinalizeAsync(finalize, TestContext.Current.CancellationToken);
        var abort = fixture.CreateAbort(replacement.PreparationId, replacement.Identity);
        await fixture.RegisterAbortGrantAsync(abort);
        var aborted = await fixture.Store.AbortAsync(abort, TestContext.Current.CancellationToken);
        rejected.ShouldBeOfType<ArtifactFinalizeRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Conflict);
        aborted.ShouldBe(new ArtifactAborted(false));
    }

    [Fact]
    public async Task FinalizeAsync_WhenImmutableVersionIsAlreadyCommitted_RejectsWithoutReplacingOrConsumingPreparation()
    {
        var fixture = new StoreFixture();
        var artifactId = new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000099"));
        var version = new ArtifactVersion("shared");
        var winner = fixture.CreatePrepare("winner"u8.ToArray(), idempotencyKey: "winner", artifactId: artifactId, version: version);
        var loser = fixture.CreatePrepare("loser"u8.ToArray(), idempotencyKey: "loser", artifactId: artifactId, version: version);
        await fixture.RegisterPrepareGrantAsync(winner);
        await fixture.RegisterPrepareGrantAsync(loser);
        _ = await fixture.Store.PrepareAsync(winner, TestContext.Current.CancellationToken);
        _ = await fixture.Store.PrepareAsync(loser, TestContext.Current.CancellationToken);
        var winnerFinalize = fixture.CreateFinalize(winner.PreparationId, winner.Identity);
        await fixture.RegisterFinalizeGrantAsync(winnerFinalize);
        var committed = (await fixture.Store.FinalizeAsync(winnerFinalize, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactFinalized>();
        var loserFinalize = fixture.CreateFinalize(loser.PreparationId, loser.Identity);
        await fixture.RegisterFinalizeGrantAsync(loserFinalize);
        var rejected = await fixture.Store.FinalizeAsync(loserFinalize, TestContext.Current.CancellationToken);
        var read = fixture.CreateRead(committed.Reference, fixture.Identity);
        await fixture.RegisterReadGrantAsync(read);
        var retained = await fixture.Store.ReadAsync(read, TestContext.Current.CancellationToken);
        var abort = fixture.CreateAbort(loser.PreparationId, loser.Identity);
        await fixture.RegisterAbortGrantAsync(abort);
        var aborted = await fixture.Store.AbortAsync(abort, TestContext.Current.CancellationToken);
        rejected.ShouldBeOfType<ArtifactFinalizeRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Conflict);
        await using var opened = retained.ShouldBeOfType<ArtifactReadOpened>();
        using var reader = new StreamReader(opened.Content);
        (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).ShouldBe("winner");
        aborted.ShouldBe(new ArtifactAborted(false));
    }

    [Fact]
    public async Task FinalizeAsync_WhenPreparationsForSameImmutableVersionRace_CommitsExactlyOne()
    {
        var fixture = new StoreFixture();
        var artifactId = new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000098"));
        var version = new ArtifactVersion("raced");
        var preparations = new[]
        {
            fixture.CreatePrepare("first"u8.ToArray(), idempotencyKey: "first", artifactId: artifactId, version: version),
            fixture.CreatePrepare("second"u8.ToArray(), idempotencyKey: "second", artifactId: artifactId, version: version),
        };
        foreach (var preparation in preparations)
        {
            await fixture.RegisterPrepareGrantAsync(preparation);
            _ = await fixture.Store.PrepareAsync(preparation, TestContext.Current.CancellationToken);
        }

        var finalizations = preparations.Select(preparation => fixture.CreateFinalize(preparation.PreparationId, preparation.Identity)).ToArray();
        foreach (var finalization in finalizations)
        {
            await fixture.RegisterFinalizeGrantAsync(finalization);
        }

        var results = await Task.WhenAll(finalizations.Select(finalization => fixture.Store.FinalizeAsync(finalization, TestContext.Current.CancellationToken).AsTask()));
        results.Count(static result => result is ArtifactFinalized).ShouldBe(1);
        results.Count(static result => result is ArtifactFinalizeRejected { Failure.Kind: ArtifactFailureKind.Conflict }).ShouldBe(1);
        var rejectedIndex = Array.FindIndex(results, static result => result is ArtifactFinalizeRejected);
        var abort = fixture.CreateAbort(preparations[rejectedIndex].PreparationId, preparations[rejectedIndex].Identity);
        await fixture.RegisterAbortGrantAsync(abort);
        (await fixture.Store.AbortAsync(abort, TestContext.Current.CancellationToken)).ShouldBe(new ArtifactAborted(false));
    }

    [Fact]
    public async Task FinalizeAsync_WhenConcurrentFreshGrantsRace_PublishesExactlyOneStableReference()
    {
        var fixture = new StoreFixture();
        var prepare = fixture.CreatePrepare("output"u8.ToArray());
        await fixture.RegisterPrepareGrantAsync(prepare);
        _ = await fixture.Store.PrepareAsync(prepare, TestContext.Current.CancellationToken);
        var requests = Enumerable.Range(0, 16).Select(_ => fixture.CreateFinalize(prepare.PreparationId, prepare.Identity)).ToArray();
        foreach (var request in requests)
        {
            await fixture.RegisterFinalizeGrantAsync(request);
        }

        var results = await Task.WhenAll(requests.Select(request => fixture.Store.FinalizeAsync(request, TestContext.Current.CancellationToken).AsTask()));
        results.ShouldAllBe(static result => result is ArtifactFinalized);
        results.Cast<ArtifactFinalized>().Select(static result => result.Reference).Distinct().Count().ShouldBe(1);
    }

    [Fact]
    public async Task PrepareAsync_WhenOtherTenantAbortedSamePreparationId_StagesIndependently()
    {
        var fixture = new StoreFixture();
        var preparationId = new ArtifactPreparationId(Guid.Parse("20000000-0000-0000-0000-000000000099"));
        var tenantA = StoreFixture.CreateIdentity("tenant-a", "principal-a");
        var tenantB = StoreFixture.CreateIdentity("tenant-b", "principal-b");
        var abort = fixture.CreateAbort(preparationId, tenantA);
        await fixture.RegisterAbortGrantAsync(abort);
        var unknown = await fixture.Store.AbortAsync(abort, TestContext.Current.CancellationToken);
        var prepare = fixture.CreatePrepare("tenant b"u8.ToArray(), idempotencyKey: "tenant-b", preparationId: preparationId, identity: tenantB);
        await fixture.RegisterPrepareGrantAsync(prepare);
        var result = await fixture.Store.PrepareAsync(prepare, TestContext.Current.CancellationToken);
        unknown.ShouldBeOfType<ArtifactAbortRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        _ = result.ShouldBeOfType<ArtifactPrepared>();
    }

    [Fact]
    public async Task AbortAsync_WhenPreparationIsUnknown_DoesNotReserveSameTenantIdentity()
    {
        var fixture = new StoreFixture();
        var preparationId = new ArtifactPreparationId(Guid.Parse("20000000-0000-0000-0000-000000000096"));
        var abort = fixture.CreateAbort(preparationId, fixture.Identity);
        await fixture.RegisterAbortGrantAsync(abort);
        var unknown = await fixture.Store.AbortAsync(abort, TestContext.Current.CancellationToken);
        var prepare = fixture.CreatePrepare("content"u8.ToArray(), idempotencyKey: "after-unknown-abort", preparationId: preparationId);
        await fixture.RegisterPrepareGrantAsync(prepare);
        var staged = await fixture.Store.PrepareAsync(prepare, TestContext.Current.CancellationToken);
        unknown.ShouldBeOfType<ArtifactAbortRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        _ = staged.ShouldBeOfType<ArtifactPrepared>();
    }

    [Fact]
    public async Task PrepareAsync_WhenDeclaredTenantDiffersFromIdentity_DeniesBeforeStateMutation()
    {
        var fixture = new StoreFixture();
        var valid = fixture.CreatePrepare("content"u8.ToArray());
        var mismatched = new ArtifactStorePrepareRequest(valid.ArtifactId, valid.PreparationId, valid.Version, valid.ProfileKey, valid.ProfileVersion, new TenantId("other"), valid.CreatedBy, valid.DirectoryId, valid.Metadata, valid.Content, valid.CreatedAt, valid.ExpiresAt, valid.Scope, valid.Identity, valid.Grant, valid.IdempotencyKey);
        await fixture.RegisterPrepareGrantAsync(mismatched);
        var rejected = await fixture.Store.PrepareAsync(mismatched, TestContext.Current.CancellationToken);
        var retry = fixture.CreatePrepare("content"u8.ToArray(), artifactId: valid.ArtifactId, version: valid.Version, preparationId: valid.PreparationId, identity: valid.Identity);
        await fixture.RegisterPrepareGrantAsync(retry);
        var accepted = await fixture.Store.PrepareAsync(retry, TestContext.Current.CancellationToken);
        rejected.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        _ = accepted.ShouldBeOfType<ArtifactPrepared>();
    }

    [Fact]
    public async Task PreparationLifecycle_WhenTenantsUseSamePreparationId_IsIndependent()
    {
        var fixture = new StoreFixture();
        var preparationId = new ArtifactPreparationId(Guid.Parse("20000000-0000-0000-0000-000000000098"));
        var tenantA = StoreFixture.CreateIdentity("tenant-a", "principal-a");
        var tenantB = StoreFixture.CreateIdentity("tenant-b", "principal-b");
        var prepareA = fixture.CreatePrepare("tenant a"u8.ToArray(), idempotencyKey: "tenant-a", preparationId: preparationId, identity: tenantA);
        var prepareB = fixture.CreatePrepare("tenant b"u8.ToArray(), idempotencyKey: "tenant-b", preparationId: preparationId, identity: tenantB);
        await fixture.RegisterPrepareGrantAsync(prepareA);
        await fixture.RegisterPrepareGrantAsync(prepareB);
        _ = await fixture.Store.PrepareAsync(prepareA, TestContext.Current.CancellationToken);
        _ = await fixture.Store.PrepareAsync(prepareB, TestContext.Current.CancellationToken);
        var finalizeA = fixture.CreateFinalize(preparationId, tenantA);
        await fixture.RegisterFinalizeGrantAsync(finalizeA);
        var abortB = fixture.CreateAbort(preparationId, tenantB);
        await fixture.RegisterAbortGrantAsync(abortB);
        var finalized = await fixture.Store.FinalizeAsync(finalizeA, TestContext.Current.CancellationToken);
        var aborted = await fixture.Store.AbortAsync(abortB, TestContext.Current.CancellationToken);
        _ = finalized.ShouldBeOfType<ArtifactFinalized>();
        aborted.ShouldBe(new ArtifactAborted(false));
    }

    [Fact]
    public async Task DeleteAsync_WhenStoredReferenceHasLegalHold_RejectsAtBackend()
    {
        var fixture = new StoreFixture();
        var reference = await fixture.CommitAsync("output"u8.ToArray(), legalHold: true);
        var delete = fixture.CreateDelete(reference, fixture.Identity);
        await fixture.RegisterDeleteGrantAsync(delete);
        var result = await fixture.Store.DeleteAsync(delete, TestContext.Current.CancellationToken);
        result.ShouldBeOfType<ArtifactDeleteRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.RetentionConflict);
    }

    private sealed class StoreFixture
    {
        private int _sequence;
        internal StoreFixture()
        {
            Clock = new FakeTimeProvider(_now);
            Grants = new InMemorySecurityGrantStore(Clock);
            Store = new InMemoryArtifactStore(Grants, Clock);
        }

        internal FakeTimeProvider Clock { get; }
        internal InMemorySecurityGrantStore Grants { get; }
        internal InMemoryArtifactStore Store { get; }
        internal ExecutionIdentity Identity { get; } = CreateIdentity("tenant", "principal");

        internal async Task<ArtifactReference> CommitAsync(byte[] bytes, bool legalHold = false)
        {
            var prepare = CreatePrepare(bytes, legalHold: legalHold);
            await RegisterPrepareGrantAsync(prepare);
            _ = await Store.PrepareAsync(prepare, TestContext.Current.CancellationToken);
            var finalize = CreateFinalize(prepare.PreparationId, prepare.Identity);
            await RegisterFinalizeGrantAsync(finalize);
            return (await Store.FinalizeAsync(finalize, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactFinalized>().Reference;
        }

        internal ArtifactStorePrepareRequest CreatePrepare(byte[] bytes, string idempotencyKey = "prepare", DateTimeOffset? expiresAt = null, InputFingerprint? grantFingerprint = null, bool legalHold = false, ArtifactId? artifactId = null, ArtifactVersion? version = null, ArtifactPreparationId? preparationId = null, ExecutionIdentity? identity = null, TenantId? tenantId = null, PrincipalId? createdBy = null, DateTimeOffset? createdAt = null, bool capturedAuthorization = false)
        {
            var selectedArtifactId = artifactId ?? new ArtifactId(NextGuid());
            var selectedPreparationId = preparationId ?? new ArtifactPreparationId(NextGuid());
            var selectedIdentity = identity ?? Identity;
            var selectedTenantId = tenantId ?? selectedIdentity.TenantId;
            var selectedCreatedBy = createdBy ?? selectedIdentity.PrincipalId;
            var selectedCreatedAt = createdAt ?? _now;
            var selectedExpiresAt = expiresAt ?? selectedCreatedAt.AddMinutes(5);
            var scope = CreateScope();
            var metadata = new ArtifactMetadata(new ArtifactOwnerId("session:owner"), "text/plain", bytes.LongLength, FileSecurityBinding.ContentFingerprint(bytes), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, legalHold));
            var grant = CreateGrant(scope, selectedIdentity, SecurityEffect.Create, [ArtifactSecurityBinding.ArtifactResource(selectedArtifactId), ArtifactSecurityBinding.PreparationResource(selectedPreparationId)], grantFingerprint ?? ArtifactSecurityBinding.PrepareFingerprint(selectedArtifactId, selectedPreparationId, version ?? new ArtifactVersion("1"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), selectedTenantId, selectedCreatedBy, new ArtifactDirectoryId("tool-output"), metadata, selectedCreatedAt, selectedExpiresAt), capturedAuthorization);
            return new ArtifactStorePrepareRequest(selectedArtifactId, selectedPreparationId, version ?? new ArtifactVersion("1"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), selectedTenantId, selectedCreatedBy, new ArtifactDirectoryId("tool-output"), metadata, [.. bytes], selectedCreatedAt, selectedExpiresAt, scope, selectedIdentity, grant, new IdempotencyKey(idempotencyKey));
        }

        internal ArtifactStoreFinalizeRequest CreateFinalize(ArtifactPreparationId preparationId, ExecutionIdentity identity)
        {
            var scope = CreateScope();
            var grant = CreateGrant(scope, identity, SecurityEffect.CreateOrReplace, [ArtifactSecurityBinding.PreparationResource(preparationId)], ArtifactSecurityBinding.FinalizeFingerprint(preparationId));
            return new ArtifactStoreFinalizeRequest(preparationId, scope, identity, grant, new IdempotencyKey($"finalize-{NextGuid()}"));
        }

        internal ArtifactStoreAbortRequest CreateAbort(ArtifactPreparationId preparationId, ExecutionIdentity identity)
        {
            var scope = CreateScope();
            var grant = CreateGrant(scope, identity, SecurityEffect.Delete, [ArtifactSecurityBinding.PreparationResource(preparationId)], ArtifactSecurityBinding.AbortFingerprint(preparationId, ArtifactAbortReason.Cancelled));
            return new ArtifactStoreAbortRequest(preparationId, ArtifactAbortReason.Cancelled, scope, identity, grant, new IdempotencyKey($"abort-{NextGuid()}"));
        }

        internal ArtifactStoreReadRequest CreateRead(ArtifactReference reference, ExecutionIdentity identity)
        {
            var scope = CreateScope();
            var grant = CreateGrant(scope, identity, SecurityEffect.Observe, [ArtifactSecurityBinding.ArtifactResource(reference.Id)], ArtifactSecurityBinding.ReadFingerprint(reference));
            return new ArtifactStoreReadRequest(reference, scope, identity, grant);
        }

        internal ArtifactStoreDeleteRequest CreateDelete(ArtifactReference reference, ExecutionIdentity identity)
        {
            var scope = CreateScope();
            var grant = CreateGrant(scope, identity, SecurityEffect.Delete, [ArtifactSecurityBinding.ArtifactResource(reference.Id)], ArtifactSecurityBinding.DeleteFingerprint(reference));
            return new ArtifactStoreDeleteRequest(reference, scope, identity, grant, new IdempotencyKey($"delete-{NextGuid()}"));
        }

        internal SecurityGrant CreateGrant(SecurityAuthorizationScope scope, ExecutionIdentity identity, SecurityEffect effect, ImmutableArray<ProtectedResource> resources, InputFingerprint fingerprint, bool capturedAuthorization = false)
        {
            var id = new GrantId(NextGuid());
            var requestId = new SecurityRequestId(NextGuid());
            var policyVersion = new SecurityPolicyVersion(1);
            var revocationVersion = new SecurityRevocationVersion(1);
            return capturedAuthorization ? new SecurityGrant(id, requestId, scope, identity, new SecurityAuthorizationContext(new SecurityProfileKey("test"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("11000000-0000-0000-0000-000000000011")), policyVersion, new ContentHash("sha256:test-policy")), new ComponentKey<ISecurityAuthority>("test"), new AgentDefinitionRevision(0), new ConfigurationVersion(1), scope, identity), Store.SecurityAudience, SecurityOperationKind.Artifact, effect, resources, fingerprint, policyVersion, revocationVersion, _now, _now.AddHours(1), 1) : new SecurityGrant(id, requestId, scope, identity, Store.SecurityAudience, SecurityOperationKind.Artifact, effect, resources, fingerprint, policyVersion, revocationVersion, _now, _now.AddHours(1), 1);
        }

        internal ValueTask RegisterPrepareGrantAsync(ArtifactStorePrepareRequest request) => Grants.RegisterAsync(request.Grant, TestContext.Current.CancellationToken);
        internal ValueTask RegisterFinalizeGrantAsync(ArtifactStoreFinalizeRequest request) => Grants.RegisterAsync(request.Grant, TestContext.Current.CancellationToken);
        internal ValueTask RegisterAbortGrantAsync(ArtifactStoreAbortRequest request) => Grants.RegisterAsync(request.Grant, TestContext.Current.CancellationToken);
        internal ValueTask RegisterReadGrantAsync(ArtifactStoreReadRequest request) => Grants.RegisterAsync(request.Grant, TestContext.Current.CancellationToken);
        internal ValueTask RegisterDeleteGrantAsync(ArtifactStoreDeleteRequest request) => Grants.RegisterAsync(request.Grant, TestContext.Current.CancellationToken);
        internal SecurityAuthorizationScope CreateScope() => new(new AgentId(NextGuid()), new SessionId(NextGuid()), new InRunOperationCorrelation(new OperationId(NextGuid()), new RunId(NextGuid()), null));
        internal static ExecutionIdentity CreateIdentity(string tenant, string principal) => TestSupport.TestExecutionIdentity.Create(new TenantId(tenant), new PrincipalId(principal), ExecutionSubjectKind.Human);
        private Guid NextGuid()
        {
            _sequence++;
            Span<byte> bytes = stackalloc byte[16];
            _ = BitConverter.TryWriteBytes(bytes, _sequence);
            return new Guid(bytes);
        }
    }

    private sealed class IntentReceiptGrantStore: ISecurityGrantStore
    {
        internal GrantConsumptionStatus Status { get; set; } = GrantConsumptionStatus.Consumed;
        internal bool IncludeReceipt { get; set; } = true;
        internal bool ReturnExactReceipt { get; set; } = true;
        internal Action? OnConsumption { get; set; }
        internal SecurityEnforcementIntent? LastIntent { get; private set; }
        internal int ConsumptionCount { get; private set; }

        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default)
        {
            _ = grant;
            _ = cancellationToken;
            return ValueTask.CompletedTask;
        }

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(SecurityGrant grant, SecurityEnforcementRequest enforcement, CancellationToken cancellationToken = default) => ValueTask.FromResult(new GrantConsumptionResult(GrantConsumptionStatus.Unknown, 0, "Legacy consumption is unsupported."));
        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(SecurityGrant grant, SecurityEnforcementRequest enforcement, SecurityEnforcementIntent intent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConsumptionCount++;
            LastIntent = intent;
            OnConsumption?.Invoke();
            var receipt = IncludeReceipt && (Status is GrantConsumptionStatus.Consumed or GrantConsumptionStatus.Reconciled) ? new SecurityEnforcementIntentReceipt(ReturnExactReceipt ? intent.Id : new SecurityEnforcementIntentId(Guid.Parse("90000000-0000-0000-0000-000000000009")), grant.Id, grant.RequestId, enforcement, intent.RequiredFence, SecurityEnforcementBinding.Fingerprint(enforcement, intent), _now) : null;
            return ValueTask.FromResult(new GrantConsumptionResult(Status, 0, Status == GrantConsumptionStatus.Consumed ? "Consumed." : "Denied.", receipt));
        }

        public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default)
        {
            _ = grantId;
            _ = cancellationToken;
            return ValueTask.FromResult(true);
        }
    }

    private static ArtifactStorePrepareRequest ChangeAttemptField(ArtifactStorePrepareRequest request, string field) => new(request.ArtifactId, request.PreparationId, field == "version" ? new ArtifactVersion("2") : request.Version, field == "profile-key" ? new ArtifactProfileKey("other") : request.ProfileKey, field == "profile-version" ? new ArtifactProfileVersion(2) : request.ProfileVersion, request.TenantId, request.CreatedBy, request.DirectoryId, request.Metadata, request.Content, field == "created-at" ? request.CreatedAt.AddSeconds(1) : request.CreatedAt, field == "expires-at" ? request.ExpiresAt.AddSeconds(1) : request.ExpiresAt, request.Scope, request.Identity, request.Grant, request.IdempotencyKey);
    private static ArtifactStorePrepareRequest WithScope(ArtifactStorePrepareRequest request, SecurityAuthorizationScope scope) => new(request.ArtifactId, request.PreparationId, request.Version, request.ProfileKey, request.ProfileVersion, request.TenantId, request.CreatedBy, request.DirectoryId, request.Metadata, request.Content, request.CreatedAt, request.ExpiresAt, scope, request.Identity, request.Grant, request.IdempotencyKey);
    private static ArtifactStorePrepareRequest WithIdentity(ArtifactStorePrepareRequest request, ExecutionIdentity identity) => new(request.ArtifactId, request.PreparationId, request.Version, request.ProfileKey, request.ProfileVersion, request.TenantId, request.CreatedBy, request.DirectoryId, request.Metadata, request.Content, request.CreatedAt, request.ExpiresAt, request.Scope, identity, request.Grant, request.IdempotencyKey);
    /// <inheritdoc/>
    protected override InMemoryArtifactStoreConformanceFixture CreateFixture() => new();
}
