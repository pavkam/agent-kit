// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Defines mandatory portable lifecycle, replay, authority, race, tenant, retention, and tombstone cases for <see cref="IArtifactStore"/>.</summary>
/// <typeparam name="TFixture">The implementation fixture composed independently for each case.</typeparam>
public abstract class ArtifactStoreConformanceTests<TFixture>
    where TFixture : IArtifactStoreConformanceFixture
{
    /// <summary>Creates a fresh fixture with isolated store and authority state.</summary>
    /// <returns>The fixture owned by one test case.</returns>
    protected abstract TFixture CreateFixture();

    /// <summary>Verifies a complete lifecycle publishes exact bytes and deletion makes the version unreadable.</summary>
    [Fact]
    public async Task Lifecycle_WhenAuthorized_RoundTripsThenDeletesExactContent()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var reference = await CommitAsync(fixture, store, "portable content"u8.ToArray(), fixture.PrimaryIdentity);

        var content = await ReadTextAsync(fixture, store, reference, fixture.PrimaryIdentity);
        var deleted = await DeleteAsync(fixture, store, reference, fixture.PrimaryIdentity);
        var missing = await ReadAsync(fixture, store, reference, fixture.PrimaryIdentity);

        content.ShouldBe("portable content");
        deleted.ShouldBe(new ArtifactStoreDeleted(false));
        missing.ShouldBeOfType<ArtifactStoreReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
    }

    /// <summary>Verifies the committed reference preserves the declared classification, ownership, retention, and integrity evidence.</summary>
    [Fact]
    public async Task FinalizeAsync_WhenPublished_ReferenceCarriesDeclaredMetadata()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var content = "evidence"u8.ToArray();
        var retention = new ArtifactRetention(new ArtifactRetentionPolicyKey("evaluation"), fixture.Now.AddDays(3), false);
        var metadata = fixture.CreateMetadata(content, retention);
        var prepare = fixture.CreatePrepare(content, metadata: metadata, version: new ArtifactVersion("7"));
        var reference = await CommitAsync(fixture, store, prepare);

        reference.Id.ShouldBe(prepare.ArtifactId);
        reference.Version.ShouldBe(new ArtifactVersion("7"));
        reference.DirectoryId.ShouldBe(prepare.DirectoryId);
        reference.ProfileKey.ShouldBe(prepare.ProfileKey);
        reference.ProfileVersion.ShouldBe(prepare.ProfileVersion);
        reference.TenantId.ShouldBe(prepare.TenantId);
        reference.CreatedBy.ShouldBe(prepare.CreatedBy);
        reference.OwnerId.ShouldBe(metadata.OwnerId);
        reference.MediaType.ShouldBe(metadata.MediaType);
        reference.Length.ShouldBe(content.LongLength);
        reference.Classification.ShouldBe(metadata.Classification);
        reference.Ownership.ShouldBe(metadata.Ownership);
        reference.Mutability.ShouldBe(metadata.Mutability);
        reference.Retention.ShouldBe(retention);
        reference.ExternalOwnership.ShouldBeNull();
        reference.Integrity.ContentHash.ShouldBe(FileSecurityBinding.ContentFingerprint(content));
        reference.CreatedAt.ShouldBe(fixture.Now);
    }

    /// <summary>Verifies equivalent replay returns the original receipt despite regenerated request identities and instants.</summary>
    [Fact]
    public async Task PrepareAsync_WhenStableIntentReplays_ReturnsOriginalReceipt()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var first = fixture.CreatePrepare("same"u8.ToArray(), idempotencyKey: "replay");
        var original = (await PrepareAsync(fixture, store, first)).ShouldBeOfType<ArtifactStorePrepared>();
        var retry = fixture.CreatePrepare(
            "same"u8.ToArray(), idempotencyKey: "replay", createdAt: first.CreatedAt.AddMinutes(1),
            lifetime: first.ExpiresAt - first.CreatedAt);

        var replay = await PrepareAsync(fixture, store, retry);

        replay.ShouldBe(original);
        replay.ShouldBeOfType<ArtifactStorePrepared>().PreparationId.ShouldBe(first.PreparationId);
    }

    /// <summary>Verifies a replay key cannot rebind a stable version or other preparation intent.</summary>
    /// <param name="changedField">The stable-intent field changed on the retry.</param>
    [Theory]
    [InlineData("content")]
    [InlineData("version")]
    [InlineData("lifetime")]
    public async Task PrepareAsync_WhenStableIntentChanges_RejectsConflict(string changedField)
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        _ = await PrepareAsync(fixture, store, fixture.CreatePrepare("same"u8.ToArray(), idempotencyKey: "conflict"));
        var changed = fixture.CreatePrepare(
            changedField == "content" ? "different"u8.ToArray() : "same"u8.ToArray(),
            idempotencyKey: "conflict",
            version: changedField == "version" ? new ArtifactVersion("2") : null,
            lifetime: changedField == "lifetime" ? TimeSpan.FromMinutes(6) : null);

        var result = await PrepareAsync(fixture, store, changed);

        result.ShouldBeOfType<ArtifactStorePrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Conflict);
    }

    /// <summary>Verifies a preparation identity cannot be reserved twice under different replay keys.</summary>
    [Fact]
    public async Task PrepareAsync_WhenPreparationIdentityIsInUse_RejectsConflict()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var preparationId = new ArtifactPreparationId(Guid.Parse("20000000-0000-0000-0000-000000000070"));
        _ = await PrepareAsync(fixture, store, fixture.CreatePrepare("one"u8.ToArray(), idempotencyKey: "one", preparationId: preparationId));

        var second = await PrepareAsync(fixture, store, fixture.CreatePrepare("two"u8.ToArray(), idempotencyKey: "two", preparationId: preparationId));

        second.ShouldBeOfType<ArtifactStorePrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Conflict);
    }

    /// <summary>Verifies altered authority evidence is denied before it can reserve preparation state.</summary>
    [Fact]
    public async Task PrepareAsync_WhenGrantFingerprintDiffers_DeniesBeforeState()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var artifactId = new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var preparationId = new ArtifactPreparationId(Guid.Parse("20000000-0000-0000-0000-000000000002"));
        var denied = fixture.CreatePrepare(
            "authority"u8.ToArray(), artifactId: artifactId, preparationId: preparationId,
            grantFingerprint: new InputFingerprint("wrong"));
        var rejection = await PrepareAsync(fixture, store, denied);

        var accepted = await PrepareAsync(
            fixture, store, fixture.CreatePrepare("authority"u8.ToArray(), artifactId: artifactId, preparationId: preparationId));

        rejection.ShouldBeOfType<ArtifactStorePrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        _ = accepted.ShouldBeOfType<ArtifactStorePrepared>();
    }

    /// <summary>Verifies one grant authorizes exactly one store effect.</summary>
    [Fact]
    public async Task PrepareAsync_WhenGrantIsReused_DeniesTheSecondEffect()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var request = fixture.CreatePrepare("once"u8.ToArray());
        await fixture.RegisterGrantAsync(request.Grant, TestContext.Current.CancellationToken);
        _ = (await store.PrepareAsync(request, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStorePrepared>();

        var reused = await store.PrepareAsync(request, TestContext.Current.CancellationToken);

        reused.ShouldBeOfType<ArtifactStorePrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
    }

    /// <summary>Verifies a separately carried tenant or creator cannot differ from the authenticated identity.</summary>
    /// <param name="field">Which partition value is forged.</param>
    [Theory]
    [InlineData("tenant")]
    [InlineData("creator")]
    public async Task PrepareAsync_WhenPartitionValueDiffersFromAuthenticatedIdentity_DeniesBeforeState(string field)
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var artifactId = new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000003"));
        var preparationId = new ArtifactPreparationId(Guid.Parse("20000000-0000-0000-0000-000000000003"));
        var forged = fixture.CreatePrepare(
            "content"u8.ToArray(), artifactId: artifactId, preparationId: preparationId,
            declaredTenant: field == "tenant" ? new TenantId("forged") : null,
            declaredCreator: field == "creator" ? new PrincipalId("forged") : null);
        var rejected = await PrepareAsync(fixture, store, forged);

        var accepted = await PrepareAsync(
            fixture, store, fixture.CreatePrepare("content"u8.ToArray(), artifactId: artifactId, preparationId: preparationId));

        rejected.ShouldBeOfType<ArtifactStorePrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        _ = accepted.ShouldBeOfType<ArtifactStorePrepared>();
    }

    /// <summary>Verifies content that disagrees with its declaration is a typed integrity mismatch that stages nothing.</summary>
    /// <param name="mismatch">Whether the declared hash or the declared length is wrong.</param>
    [Theory]
    [InlineData("hash")]
    [InlineData("length")]
    public async Task PrepareAsync_WhenContentDiffersFromDeclaration_RejectsWithIntegrityMismatch(string mismatch)
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var content = "original"u8.ToArray();
        var metadata = mismatch == "hash"
            ? fixture.CreateMetadata(content, declaredContentHash: FileSecurityBinding.ContentFingerprint("other"u8))
            : fixture.CreateMetadata(content, declaredLength: content.Length + 1);
        var preparationId = new ArtifactPreparationId(Guid.Parse("20000000-0000-0000-0000-000000000004"));
        var rejected = await PrepareAsync(
            fixture, store, fixture.CreatePrepare(content, preparationId: preparationId, metadata: metadata, idempotencyKey: "tampered"));

        var retry = await PrepareAsync(
            fixture, store, fixture.CreatePrepare(content, preparationId: preparationId, idempotencyKey: "valid"));

        rejected.ShouldBeOfType<ArtifactStorePrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.IntegrityMismatch);
        _ = retry.ShouldBeOfType<ArtifactStorePrepared>();
    }

    /// <summary>Verifies omitting the declared hash records the observed SHA-256 fingerprint.</summary>
    [Fact]
    public async Task FinalizeAsync_WhenNoHashWasDeclared_RecordsObservedFingerprint()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var content = "undeclared"u8.ToArray();
        var declared = fixture.CreateMetadata(content);
        var metadata = new ArtifactMetadata(
            declared.OwnerId, declared.MediaType, declared.DeclaredLength, null, declared.Classification,
            declared.Ownership, declared.Mutability, declared.Retention, null);

        var reference = await CommitAsync(fixture, store, fixture.CreatePrepare(content, metadata: metadata));

        reference.Integrity.ContentHash.ShouldBe(FileSecurityBinding.ContentFingerprint(content));
    }

    /// <summary>Verifies identical raw artifact identities remain independent across tenant partitions.</summary>
    [Fact]
    public async Task Lifecycle_WhenTenantsShareRawArtifactIdentity_IsolatesContentAndDeletion()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var id = new ArtifactId(Guid.Parse("30000000-0000-0000-0000-000000000003"));
        var preparationId = new ArtifactPreparationId(Guid.Parse("40000000-0000-0000-0000-000000000004"));
        var version = new ArtifactVersion("1");
        var referenceA = await CommitAsync(fixture, store, "tenant a"u8.ToArray(), fixture.PrimaryIdentity, id, preparationId, version);
        var referenceB = await CommitAsync(fixture, store, "tenant b"u8.ToArray(), fixture.SecondaryIdentity, id, preparationId, version);
        var initialContentA = await ReadTextAsync(fixture, store, referenceA, fixture.PrimaryIdentity);
        var initialContentB = await ReadTextAsync(fixture, store, referenceB, fixture.SecondaryIdentity);

        _ = (await DeleteAsync(fixture, store, referenceA, fixture.PrimaryIdentity)).ShouldBeOfType<ArtifactStoreDeleted>();
        var missingA = await ReadAsync(fixture, store, referenceA, fixture.PrimaryIdentity);
        var contentB = await ReadTextAsync(fixture, store, referenceB, fixture.SecondaryIdentity);

        initialContentA.ShouldBe("tenant a");
        initialContentB.ShouldBe("tenant b");
        referenceA.Id.ShouldBe(referenceB.Id);
        referenceA.Version.ShouldBe(referenceB.Version);
        referenceA.TenantId.ShouldNotBe(referenceB.TenantId);
        missingA.ShouldBeOfType<ArtifactStoreReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        contentB.ShouldBe("tenant b");
    }

    /// <summary>Verifies another tenant cannot finalize, abort, read, or delete content it does not own, and cannot discover it.</summary>
    [Fact]
    public async Task Lifecycle_WhenForeignTenantTargetsOwnersState_ReturnsNotFoundAndLeavesItIntact()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepare = fixture.CreatePrepare("owned"u8.ToArray());
        _ = await PrepareAsync(fixture, store, prepare);

        var foreignFinalize = await FinalizeAsync(fixture, store, prepare.PreparationId, fixture.SecondaryIdentity);
        var foreignAbort = await AbortAsync(fixture, store, prepare.PreparationId, fixture.SecondaryIdentity);
        var reference = (await FinalizeAsync(fixture, store, prepare.PreparationId, fixture.PrimaryIdentity))
            .ShouldBeOfType<ArtifactStoreFinalized>().Reference;
        var foreignRead = await ReadAsync(fixture, store, reference, fixture.SecondaryIdentity);
        var foreignDelete = await DeleteAsync(fixture, store, reference, fixture.SecondaryIdentity);
        var ownerContent = await ReadTextAsync(fixture, store, reference, fixture.PrimaryIdentity);

        foreignFinalize.ShouldBeOfType<ArtifactStoreFinalizeRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        foreignAbort.ShouldBeOfType<ArtifactStoreAbortRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        foreignRead.ShouldBeOfType<ArtifactStoreReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        foreignDelete.ShouldBeOfType<ArtifactStoreDeleteRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        ownerContent.ShouldBe("owned");
    }

    /// <summary>Verifies a foreign tenant learns nothing about a deleted owner's abort receipt.</summary>
    [Fact]
    public async Task AbortAsync_WhenFinalizedArtifactWasDeleted_HidesReceiptFromForeignTenant()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepare = fixture.CreatePrepare("complete output"u8.ToArray());
        var reference = await CommitAsync(fixture, store, prepare);
        _ = await DeleteAsync(fixture, store, reference, fixture.PrimaryIdentity);

        var foreign = await AbortAsync(fixture, store, prepare.PreparationId, fixture.SecondaryIdentity);
        var owner = await AbortAsync(fixture, store, prepare.PreparationId, fixture.PrimaryIdentity);

        foreign.ShouldBeOfType<ArtifactStoreAbortRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        owner.ShouldBe(new ArtifactStoreAborted(true));
    }

    /// <summary>Verifies competing finalize and abort operations establish exactly one terminal preparation outcome.</summary>
    [Fact]
    public async Task FinalizeAndAbortAsync_WhenTheyRace_ProduceOneTerminalOutcome()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var prepare = fixture.CreatePrepare("race"u8.ToArray(), idempotencyKey: $"race-{attempt}");
            _ = await PrepareAsync(fixture, store, prepare);
            var finalize = fixture.CreateFinalize(prepare.PreparationId, fixture.PrimaryIdentity);
            var abort = fixture.CreateAbort(prepare.PreparationId, fixture.PrimaryIdentity);
            await fixture.RegisterGrantAsync(finalize.Grant, TestContext.Current.CancellationToken);
            await fixture.RegisterGrantAsync(abort.Grant, TestContext.Current.CancellationToken);

            var operations = await Task.WhenAll(
                Task.Run(async () => (object) await store.FinalizeAsync(finalize, TestContext.Current.CancellationToken)),
                Task.Run(async () => (object) await store.AbortAsync(abort, TestContext.Current.CancellationToken)));

            operations.Count(static result => result is ArtifactStoreFinalized or ArtifactStoreAborted).ShouldBe(1);
            if (operations.OfType<ArtifactStoreFinalized>().Any())
            {
                operations.OfType<ArtifactStoreAbortRejected>().Single().Failure.Kind.ShouldBe(ArtifactFailureKind.Conflict);
            }
            else
            {
                operations.OfType<ArtifactStoreFinalizeRejected>().Single().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
            }
        }
    }

    /// <summary>Verifies an equivalent finalize retry returns the same immutable reference, even under concurrent fresh grants.</summary>
    [Fact]
    public async Task FinalizeAsync_WhenEquivalentRetriesRace_PublishOneStableReference()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepare = fixture.CreatePrepare("output"u8.ToArray());
        _ = await PrepareAsync(fixture, store, prepare);
        var requests = Enumerable.Range(0, 12).Select(_ => fixture.CreateFinalize(prepare.PreparationId, fixture.PrimaryIdentity)).ToArray();
        foreach (var request in requests)
        {
            await fixture.RegisterGrantAsync(request.Grant, TestContext.Current.CancellationToken);
        }

        var results = await Task.WhenAll(requests.Select(request => Task.Run(
            async () => await store.FinalizeAsync(request, TestContext.Current.CancellationToken))));
        var later = await FinalizeAsync(fixture, store, prepare.PreparationId, fixture.PrimaryIdentity);

        results.ShouldAllBe(static result => result is ArtifactStoreFinalized);
        results.Cast<ArtifactStoreFinalized>().Select(static result => result.Reference).Distinct().Count().ShouldBe(1);
        later.ShouldBe(results[0]);
    }

    /// <summary>Verifies competing preparations of one immutable version have exactly one publication winner and a losing preparation stays abortable.</summary>
    [Fact]
    public async Task FinalizeAsync_WhenPreparationsForSameImmutableVersionRace_CommitsExactlyOne()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var artifactId = new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000098"));
        var version = new ArtifactVersion("raced");
        var preparations = new[]
        {
            fixture.CreatePrepare("first"u8.ToArray(), idempotencyKey: "first", artifactId: artifactId, version: version),
            fixture.CreatePrepare("second"u8.ToArray(), idempotencyKey: "second", artifactId: artifactId, version: version),
        };
        foreach (var preparation in preparations)
        {
            _ = await PrepareAsync(fixture, store, preparation);
        }

        var finalizations = preparations.Select(preparation => fixture.CreateFinalize(preparation.PreparationId, fixture.PrimaryIdentity)).ToArray();
        foreach (var finalization in finalizations)
        {
            await fixture.RegisterGrantAsync(finalization.Grant, TestContext.Current.CancellationToken);
        }

        var results = await Task.WhenAll(finalizations.Select(finalization => Task.Run(
            async () => await store.FinalizeAsync(finalization, TestContext.Current.CancellationToken))));

        results.Count(static result => result is ArtifactStoreFinalized).ShouldBe(1);
        results.Count(static result => result is ArtifactStoreFinalizeRejected { Failure.Kind: ArtifactFailureKind.Conflict }).ShouldBe(1);
        var loser = Array.FindIndex(results, static result => result is ArtifactStoreFinalizeRejected);
        (await AbortAsync(fixture, store, preparations[loser].PreparationId, fixture.PrimaryIdentity)).ShouldBe(new ArtifactStoreAborted(false));
    }

    /// <summary>Verifies an already committed immutable version is never replaced by a later preparation.</summary>
    [Fact]
    public async Task FinalizeAsync_WhenImmutableVersionIsAlreadyCommitted_RejectsWithoutReplacingContent()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var artifactId = new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000099"));
        var version = new ArtifactVersion("shared");
        var committed = await CommitAsync(fixture, store, "winner"u8.ToArray(), fixture.PrimaryIdentity, artifactId, version: version);
        var loser = fixture.CreatePrepare("loser"u8.ToArray(), idempotencyKey: "loser", artifactId: artifactId, version: version);
        _ = await PrepareAsync(fixture, store, loser);

        var rejected = await FinalizeAsync(fixture, store, loser.PreparationId, fixture.PrimaryIdentity);
        var retained = await ReadTextAsync(fixture, store, committed, fixture.PrimaryIdentity);
        var aborted = await AbortAsync(fixture, store, loser.PreparationId, fixture.PrimaryIdentity);

        rejected.ShouldBeOfType<ArtifactStoreFinalizeRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Conflict);
        retained.ShouldBe("winner");
        aborted.ShouldBe(new ArtifactStoreAborted(false));
    }

    /// <summary>Verifies a tombstone prevents a new preparation from rebinding a deleted immutable version.</summary>
    [Fact]
    public async Task FinalizeAsync_WhenVersionWasDeleted_TombstonePreventsRebind()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var reference = await CommitAsync(fixture, store, "original"u8.ToArray(), fixture.PrimaryIdentity);
        _ = await DeleteAsync(fixture, store, reference, fixture.PrimaryIdentity);
        var replacement = fixture.CreatePrepare(
            "replacement"u8.ToArray(), idempotencyKey: "replacement", artifactId: reference.Id, version: reference.Version);
        _ = await PrepareAsync(fixture, store, replacement);

        var rejected = await FinalizeAsync(fixture, store, replacement.PreparationId, fixture.PrimaryIdentity);
        var stale = await ReadAsync(fixture, store, reference, fixture.PrimaryIdentity);
        var aborted = await AbortAsync(fixture, store, replacement.PreparationId, fixture.PrimaryIdentity);

        rejected.ShouldBeOfType<ArtifactStoreFinalizeRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Conflict);
        stale.ShouldBeOfType<ArtifactStoreReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        aborted.ShouldBe(new ArtifactStoreAborted(false));
    }

    /// <summary>Verifies an expired preparation never publishes, and its staging is gone.</summary>
    [Fact]
    public async Task FinalizeAsync_WhenPreparationExpired_RejectsAndNeverPublishes()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepare = fixture.CreatePrepare("output"u8.ToArray(), lifetime: TimeSpan.FromSeconds(1));
        _ = await PrepareAsync(fixture, store, prepare);
        fixture.Advance(TimeSpan.FromSeconds(2));

        var first = await FinalizeAsync(fixture, store, prepare.PreparationId, fixture.PrimaryIdentity);
        var second = await FinalizeAsync(fixture, store, prepare.PreparationId, fixture.PrimaryIdentity);

        first.ShouldBeOfType<ArtifactStoreFinalizeRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        second.ShouldBeOfType<ArtifactStoreFinalizeRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
    }

    /// <summary>Verifies finalize after the committed version was deleted is unavailable rather than resurrecting bytes.</summary>
    [Fact]
    public async Task FinalizeAsync_WhenReplayedAfterDelete_RejectsWithNotFound()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepare = fixture.CreatePrepare("output"u8.ToArray());
        var reference = await CommitAsync(fixture, store, prepare);
        _ = await DeleteAsync(fixture, store, reference, fixture.PrimaryIdentity);

        var replay = await FinalizeAsync(fixture, store, prepare.PreparationId, fixture.PrimaryIdentity);

        replay.ShouldBeOfType<ArtifactStoreFinalizeRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
    }

    /// <summary>Verifies a committed preparation is never deleted as though it were unfinished staging.</summary>
    [Fact]
    public async Task AbortAsync_WhenPreparationWasCommitted_RejectsWithoutDeletingCommit()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepare = fixture.CreatePrepare("output"u8.ToArray());
        var reference = await CommitAsync(fixture, store, prepare);

        var rejected = await AbortAsync(fixture, store, prepare.PreparationId, fixture.PrimaryIdentity);
        var content = await ReadTextAsync(fixture, store, reference, fixture.PrimaryIdentity);
        var replay = await FinalizeAsync(fixture, store, prepare.PreparationId, fixture.PrimaryIdentity);

        rejected.ShouldBeOfType<ArtifactStoreAbortRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Conflict);
        content.ShouldBe("output");
        replay.ShouldBe(new ArtifactStoreFinalized(reference));
    }

    /// <summary>Verifies abort is idempotent and an unknown abort reserves no identity.</summary>
    [Fact]
    public async Task AbortAsync_WhenAbortedAgainOrUnknown_IsIdempotentAndReservesNothing()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepare = fixture.CreatePrepare("output"u8.ToArray());
        _ = await PrepareAsync(fixture, store, prepare);
        var first = await AbortAsync(fixture, store, prepare.PreparationId, fixture.PrimaryIdentity);
        var second = await AbortAsync(fixture, store, prepare.PreparationId, fixture.PrimaryIdentity);
        var unknownId = new ArtifactPreparationId(Guid.Parse("20000000-0000-0000-0000-000000000096"));
        var unknown = await AbortAsync(fixture, store, unknownId, fixture.PrimaryIdentity);
        var staged = await PrepareAsync(fixture, store, fixture.CreatePrepare("late"u8.ToArray(), idempotencyKey: "late", preparationId: unknownId));

        first.ShouldBe(new ArtifactStoreAborted(false));
        second.ShouldBe(new ArtifactStoreAborted(true));
        unknown.ShouldBeOfType<ArtifactStoreAbortRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        _ = staged.ShouldBeOfType<ArtifactStorePrepared>();
    }

    /// <summary>Verifies an aborted preparation can no longer be published.</summary>
    [Fact]
    public async Task FinalizeAsync_WhenPreparationWasAborted_RejectsWithNotFound()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepare = fixture.CreatePrepare("output"u8.ToArray());
        _ = await PrepareAsync(fixture, store, prepare);
        _ = await AbortAsync(fixture, store, prepare.PreparationId, fixture.PrimaryIdentity);

        var result = await FinalizeAsync(fixture, store, prepare.PreparationId, fixture.PrimaryIdentity);

        result.ShouldBeOfType<ArtifactStoreFinalizeRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
    }

    /// <summary>Verifies unpublished staging is never readable as committed content.</summary>
    [Fact]
    public async Task ReadAsync_WhenOnlyPrepared_NeverExposesStaging()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var committed = await CommitAsync(fixture, store, "committed"u8.ToArray(), fixture.PrimaryIdentity);
        var prepare = fixture.CreatePrepare("staged"u8.ToArray(), idempotencyKey: "staged");
        _ = await PrepareAsync(fixture, store, prepare);
        var forged = new ArtifactReference(
            prepare.ArtifactId, prepare.Version, prepare.DirectoryId, prepare.ProfileKey, prepare.ProfileVersion,
            prepare.TenantId, prepare.Metadata.OwnerId, prepare.CreatedBy, prepare.Metadata.MediaType,
            prepare.Metadata.DeclaredLength, new ArtifactIntegrity(prepare.ContentHash, fixture.Now),
            prepare.Metadata.Classification, prepare.Metadata.Ownership, prepare.Metadata.Mutability,
            prepare.Metadata.Retention, null, fixture.Now);

        var result = await ReadAsync(fixture, store, forged, fixture.PrimaryIdentity);

        result.ShouldBeOfType<ArtifactStoreReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        (await ReadTextAsync(fixture, store, committed, fixture.PrimaryIdentity)).ShouldBe("committed");
    }

    /// <summary>Verifies a caller-supplied reference cannot redefine the stored canonical reference.</summary>
    [Fact]
    public async Task ReadAsync_WhenReferenceDiffersFromStoredReference_ReturnsNotFound()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var reference = await CommitAsync(fixture, store, "output"u8.ToArray(), fixture.PrimaryIdentity);
        var altered = Altered(reference, ownerId: new ArtifactOwnerId("conformance:attacker"));

        var result = await ReadAsync(fixture, store, altered, fixture.PrimaryIdentity);

        result.ShouldBeOfType<ArtifactStoreReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
    }

    /// <summary>Verifies a grant bound to one reference cannot read a different concrete reference.</summary>
    [Fact]
    public async Task ReadAsync_WhenGrantWasBoundToDifferentReference_DeniesWithoutChangingContent()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var reference = await CommitAsync(fixture, store, "complete output"u8.ToArray(), fixture.PrimaryIdentity);
        var authorized = fixture.CreateRead(reference, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(authorized.Grant, TestContext.Current.CancellationToken);
        var changed = new ArtifactStoreReadRequest(
            Altered(reference, directoryId: new ArtifactDirectoryId("different-directory")), authorized.Grant);

        var rejected = await store.ReadAsync(changed, TestContext.Current.CancellationToken);
        var content = await ReadTextAsync(fixture, store, reference, fixture.PrimaryIdentity);

        rejected.ShouldBeOfType<ArtifactStoreReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        content.ShouldBe("complete output");
    }

    /// <summary>Verifies deletion is idempotent, exact, and preserves replay evidence against altered references.</summary>
    [Fact]
    public async Task DeleteAsync_WhenReplayedOrAltered_ReturnsAlreadyAbsentOnlyForTheExactReference()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var reference = await CommitAsync(fixture, store, "complete output"u8.ToArray(), fixture.PrimaryIdentity);
        var first = await DeleteAsync(fixture, store, reference, fixture.PrimaryIdentity);
        var replay = await DeleteAsync(fixture, store, reference, fixture.PrimaryIdentity);
        var altered = await DeleteAsync(
            fixture, store, Altered(reference, directoryId: new ArtifactDirectoryId("different-directory")), fixture.PrimaryIdentity);
        var exact = await DeleteAsync(fixture, store, reference, fixture.PrimaryIdentity);

        first.ShouldBe(new ArtifactStoreDeleted(false));
        replay.ShouldBe(new ArtifactStoreDeleted(true));
        altered.ShouldBeOfType<ArtifactStoreDeleteRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        exact.ShouldBe(new ArtifactStoreDeleted(true));
    }

    /// <summary>Verifies legal hold rejects deletion at the backend and leaves the content readable.</summary>
    [Fact]
    public async Task DeleteAsync_WhenStoredReferenceHasLegalHold_RejectsAndContentRemains()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var content = "held"u8.ToArray();
        var held = fixture.CreateMetadata(content, new ArtifactRetention(new ArtifactRetentionPolicyKey("hold"), null, true));
        var reference = await CommitAsync(fixture, store, fixture.CreatePrepare(content, metadata: held));

        var result = await DeleteAsync(fixture, store, reference, fixture.PrimaryIdentity);
        var retained = await ReadTextAsync(fixture, store, reference, fixture.PrimaryIdentity);

        result.ShouldBeOfType<ArtifactStoreDeleteRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.RetentionConflict);
        retained.ShouldBe("held");
    }

    /// <summary>Verifies the stored hold, not a caller-supplied reference, governs deletion.</summary>
    [Fact]
    public async Task DeleteAsync_WhenCallerClearsLegalHoldOnSuppliedReference_StoredHoldStillGoverns()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var content = "held"u8.ToArray();
        var held = fixture.CreateMetadata(content, new ArtifactRetention(new ArtifactRetentionPolicyKey("hold"), null, true));
        var reference = await CommitAsync(fixture, store, fixture.CreatePrepare(content, metadata: held));
        var cleared = Altered(reference, retention: new ArtifactRetention(reference.Retention.Policy, null, false));

        var result = await DeleteAsync(fixture, store, cleared, fixture.PrimaryIdentity);
        var retained = await ReadTextAsync(fixture, store, reference, fixture.PrimaryIdentity);

        result.ShouldBeOfType<ArtifactStoreDeleteRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        retained.ShouldBe("held");
    }

    /// <summary>Verifies external ownership without delegated delete authority is never deleted by the store.</summary>
    [Fact]
    public async Task DeleteAsync_WhenExternalOwnerKeepsDeleteAuthority_RejectsAndNeverDeletes()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var content = "external"u8.ToArray();
        var ownership = new ExternalArtifactOwnership(
            new ExternalArtifactResourceId("vendor:file-1"), new Uri("https://files.example.com/objects/1"), false);
        var reference = await CommitAsync(fixture, store, fixture.CreatePrepare(content, metadata: fixture.CreateMetadata(content, externalOwnership: ownership)));

        var result = await DeleteAsync(fixture, store, reference, fixture.PrimaryIdentity);
        var retained = await ReadTextAsync(fixture, store, reference, fixture.PrimaryIdentity);

        reference.ExternalOwnership.ShouldBe(ownership);
        reference.Ownership.ShouldBe(ArtifactOwnershipKind.External);
        result.ShouldBeOfType<ArtifactStoreDeleteRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.RetentionConflict);
        retained.ShouldBe("external");
    }

    /// <summary>Verifies delegated delete authority lets the store remove only its own record of external content.</summary>
    [Fact]
    public async Task DeleteAsync_WhenExternalOwnerDelegatesDelete_RemovesOnlyTheStoredRecord()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var content = "external"u8.ToArray();
        var ownership = new ExternalArtifactOwnership(
            new ExternalArtifactResourceId("vendor:file-2"), new Uri("https://files.example.com/objects/2"), true);
        var reference = await CommitAsync(fixture, store, fixture.CreatePrepare(content, metadata: fixture.CreateMetadata(content, externalOwnership: ownership)));

        var result = await DeleteAsync(fixture, store, reference, fixture.PrimaryIdentity);
        var missing = await ReadAsync(fixture, store, reference, fixture.PrimaryIdentity);

        result.ShouldBe(new ArtifactStoreDeleted(false));
        missing.ShouldBeOfType<ArtifactStoreReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
    }

    /// <summary>Verifies an unregistered or unauthorized grant is denied before any state changes, for every operation.</summary>
    [Fact]
    public async Task Lifecycle_WhenGrantIsNotRegistered_DeniesWithoutMutatingState()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepare = fixture.CreatePrepare("output"u8.ToArray());
        var deniedPrepare = await store.PrepareAsync(prepare, TestContext.Current.CancellationToken);
        _ = await PrepareAsync(fixture, store, fixture.CreatePrepare("output"u8.ToArray(), preparationId: prepare.PreparationId, artifactId: prepare.ArtifactId));

        var deniedFinalize = await store.FinalizeAsync(
            fixture.CreateFinalize(prepare.PreparationId, fixture.PrimaryIdentity), TestContext.Current.CancellationToken);
        var deniedAbort = await store.AbortAsync(
            fixture.CreateAbort(prepare.PreparationId, fixture.PrimaryIdentity), TestContext.Current.CancellationToken);
        var reference = (await FinalizeAsync(fixture, store, prepare.PreparationId, fixture.PrimaryIdentity))
            .ShouldBeOfType<ArtifactStoreFinalized>().Reference;
        var deniedRead = await store.ReadAsync(
            fixture.CreateRead(reference, fixture.PrimaryIdentity), TestContext.Current.CancellationToken);
        var deniedDelete = await store.DeleteAsync(
            fixture.CreateDelete(reference, fixture.PrimaryIdentity), TestContext.Current.CancellationToken);

        deniedPrepare.ShouldBeOfType<ArtifactStorePrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        deniedFinalize.ShouldBeOfType<ArtifactStoreFinalizeRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        deniedAbort.ShouldBeOfType<ArtifactStoreAbortRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        deniedRead.ShouldBeOfType<ArtifactStoreReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        deniedDelete.ShouldBeOfType<ArtifactStoreDeleteRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        (await ReadTextAsync(fixture, store, reference, fixture.PrimaryIdentity)).ShouldBe("output");
    }

    /// <summary>Verifies cancellation before an effect leaves no state behind.</summary>
    [Fact]
    public async Task PrepareAsync_WhenCancelledBeforeTheEffect_StagesNothing()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var preparationId = new ArtifactPreparationId(Guid.Parse("20000000-0000-0000-0000-000000000095"));
        var request = fixture.CreatePrepare("cancelled"u8.ToArray(), preparationId: preparationId);
        await fixture.RegisterGrantAsync(request.Grant, TestContext.Current.CancellationToken);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var action = async () => await store.PrepareAsync(request, cancelled.Token);
        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        var abort = await AbortAsync(fixture, store, preparationId, fixture.PrimaryIdentity);

        abort.ShouldBeOfType<ArtifactStoreAbortRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
    }

    /// <summary>Creates and publishes content with exact grants supplied by the fixture authority.</summary>
    private static async Task<ArtifactReference> CommitAsync(
        TFixture fixture,
        IArtifactStore store,
        byte[] content,
        ExecutionIdentity identity,
        ArtifactId? artifactId = null,
        ArtifactPreparationId? preparationId = null,
        ArtifactVersion? version = null) =>
        await CommitAsync(
            fixture, store,
            fixture.CreatePrepare(content, identity, artifactId: artifactId, preparationId: preparationId, version: version));

    /// <summary>Stages then publishes one prepared request and checks the reference identity.</summary>
    private static async Task<ArtifactReference> CommitAsync(TFixture fixture, IArtifactStore store, ArtifactStorePrepareRequest prepare)
    {
        var prepared = (await PrepareAsync(fixture, store, prepare)).ShouldBeOfType<ArtifactStorePrepared>();
        var identity = prepare.Identity;
        var reference = (await FinalizeAsync(fixture, store, prepared.PreparationId, identity))
            .ShouldBeOfType<ArtifactStoreFinalized>().Reference;
        reference.Id.ShouldBe(prepared.ArtifactId);
        reference.Version.ShouldBe(prepare.Version);
        reference.Integrity.ContentHash.ShouldBe(FileSecurityBinding.ContentFingerprint(prepare.Content.AsSpan()));
        reference.TenantId.ShouldBe(identity.TenantId);
        return reference;
    }

    private static async Task<ArtifactStorePrepareResult> PrepareAsync(TFixture fixture, IArtifactStore store, ArtifactStorePrepareRequest request)
    {
        await fixture.RegisterGrantAsync(request.Grant, TestContext.Current.CancellationToken);
        return await store.PrepareAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<ArtifactStoreFinalizeResult> FinalizeAsync(
        TFixture fixture, IArtifactStore store, ArtifactPreparationId preparationId, ExecutionIdentity identity)
    {
        var request = fixture.CreateFinalize(preparationId, identity);
        await fixture.RegisterGrantAsync(request.Grant, TestContext.Current.CancellationToken);
        return await store.FinalizeAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<ArtifactStoreAbortResult> AbortAsync(
        TFixture fixture, IArtifactStore store, ArtifactPreparationId preparationId, ExecutionIdentity identity)
    {
        var request = fixture.CreateAbort(preparationId, identity);
        await fixture.RegisterGrantAsync(request.Grant, TestContext.Current.CancellationToken);
        return await store.AbortAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<ArtifactStoreReadResult> ReadAsync(
        TFixture fixture, IArtifactStore store, ArtifactReference reference, ExecutionIdentity identity)
    {
        var request = fixture.CreateRead(reference, identity);
        await fixture.RegisterGrantAsync(request.Grant, TestContext.Current.CancellationToken);
        return await store.ReadAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<ArtifactStoreDeleteResult> DeleteAsync(
        TFixture fixture, IArtifactStore store, ArtifactReference reference, ExecutionIdentity identity)
    {
        var request = fixture.CreateDelete(reference, identity);
        await fixture.RegisterGrantAsync(request.Grant, TestContext.Current.CancellationToken);
        return await store.DeleteAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Reads and closes an owned artifact stream before a later lifecycle operation begins.</summary>
    private static async Task<string> ReadTextAsync(
        TFixture fixture, IArtifactStore store, ArtifactReference reference, ExecutionIdentity identity)
    {
        await using var opened = (await ReadAsync(fixture, store, reference, identity)).ShouldBeOfType<ArtifactStoreReadOpened>();
        using var reader = new StreamReader(opened.Content);
        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }

    private static ArtifactReference Altered(
        ArtifactReference reference,
        ArtifactOwnerId? ownerId = null,
        ArtifactDirectoryId? directoryId = null,
        ArtifactRetention? retention = null) => new(
            reference.Id, reference.Version, directoryId ?? reference.DirectoryId, reference.ProfileKey,
            reference.ProfileVersion, reference.TenantId, ownerId ?? reference.OwnerId, reference.CreatedBy,
            reference.MediaType, reference.Length, reference.Integrity, reference.Classification,
            reference.Ownership, reference.Mutability, retention ?? reference.Retention,
            reference.ExternalOwnership, reference.CreatedAt);
}
