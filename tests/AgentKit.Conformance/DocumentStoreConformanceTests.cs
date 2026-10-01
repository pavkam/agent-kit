// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.TestSupport;

/// <summary>Defines portable versioned-publication, active-pointer, deletion, isolation, and authorization behavior for <see cref="IDocumentStore"/>.</summary>
/// <typeparam name="TFixture">The adapter-specific isolated fixture.</typeparam>
/// <remarks>Every case is required contract behavior for all document-store adapters. Durability cases run when the fixture declares <see cref="ConformanceCapabilities.SupportsDurability"/>.</remarks>
public abstract class DocumentStoreConformanceTests<TFixture>
    where TFixture : IDocumentStoreConformanceFixture, new()
{
    /// <summary>Verifies a staged version is stored but not retrievable as the active version.</summary>
    [Fact]
    public async Task WriteAsync_WhenStagingWithoutActivation_StoresTheVersionWithoutMakingItActive()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner);
        var chunks = MemoryTestData.Chunks(record, 3);

        var written = await WriteAsync(fixture, owner, record, chunks, activate: false);

        written.IsWritten.ShouldBeTrue();
        written.State.ShouldBe(DocumentVersionState.Staged);
        written.PreviousActiveVersion.ShouldBeNull();
        (await ReadAsync(fixture, owner, record.Id, null, false)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
        var staged = await ReadAsync(fixture, owner, record.Id, record.Version.Value, true);
        staged.State.ShouldBe(DocumentVersionState.Staged);
        staged.Chunks.ShouldBe(chunks);
        staged.ActiveVersion.ShouldBeNull();
    }

    /// <summary>Verifies publishing with activation makes the whole chunk set the active version atomically.</summary>
    [Fact]
    public async Task WriteAsync_WhenActivating_MakesTheCompleteChunkSetActive()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner);
        var chunks = MemoryTestData.Chunks(record, 4);

        var written = await WriteAsync(fixture, owner, record, chunks, activate: true);

        written.State.ShouldBe(DocumentVersionState.Active);
        var read = await ReadAsync(fixture, owner, record.Id, null, true);
        read.Record.ShouldBe(record);
        read.Chunks.ShouldBe(chunks);
        read.ActiveVersion.ShouldBe(record.Version);
        read.State.ShouldBe(DocumentVersionState.Active);
    }

    /// <summary>Verifies an equivalent publication replay returns the stored version without duplicating it.</summary>
    [Fact]
    public async Task WriteAsync_WhenReplayedWithTheSameKey_ReturnsTheStoredVersion()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner);
        var chunks = MemoryTestData.Chunks(record);
        _ = await WriteAsync(fixture, owner, record, chunks, true, "k");

        var replay = await WriteAsync(fixture, owner, record, chunks, true, "k");

        replay.IsWritten.ShouldBeTrue();
        replay.Replayed.ShouldBeTrue();
        replay.State.ShouldBe(DocumentVersionState.Active);
    }

    /// <summary>Verifies the same version cannot be published twice under different keys.</summary>
    [Fact]
    public async Task WriteAsync_WhenTheVersionAlreadyExistsUnderAnotherKey_RejectsWithIdempotencyConflict()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner);
        var chunks = MemoryTestData.Chunks(record);
        _ = await WriteAsync(fixture, owner, record, chunks, true, "k1");

        (await WriteAsync(fixture, owner, record, chunks, true, "k2")).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.IdempotencyConflict);
    }

    /// <summary>Verifies reusing a key for different content is refused.</summary>
    [Fact]
    public async Task WriteAsync_WhenKeyIsReusedForDifferentContent_RejectsWithIdempotencyConflict()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner);
        _ = await WriteAsync(fixture, owner, record, MemoryTestData.Chunks(record), true, "k");
        var other = MemoryTestData.Document(owner, "v2", "sha256:bb", record.Id);

        (await WriteAsync(fixture, owner, other, MemoryTestData.Chunks(other), true, "k")).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.IdempotencyConflict);
    }

    /// <summary>Verifies publishing a newer version with activation leaves only the newer version active.</summary>
    [Fact]
    public async Task WriteAsync_WhenANewerVersionIsActivated_NeverLeavesStaleAndCurrentBothActive()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var v1 = MemoryTestData.Document(owner, "v1", "sha256:aa");
        var v2 = MemoryTestData.Document(owner, "v2", "sha256:bb", v1.Id);
        _ = await WriteAsync(fixture, owner, v1, MemoryTestData.Chunks(v1, 2), true, "k1");

        var second = await WriteAsync(fixture, owner, v2, MemoryTestData.Chunks(v2, 3), true, "k2");

        second.PreviousActiveVersion.ShouldBe(v1.Version);
        var active = await ReadAsync(fixture, owner, v1.Id, null, true);
        active.Record!.Version.ShouldBe(v2.Version);
        active.Chunks.Length.ShouldBe(3);
        active.Chunks.ShouldAllBe(chunk => chunk.Version == v2.Version);
        var old = await ReadAsync(fixture, owner, v1.Id, "v1", true);
        old.State.ShouldBe(DocumentVersionState.Superseded);
        old.ActiveVersion.ShouldBe(v2.Version);
    }

    /// <summary>Verifies a staged version can be activated by a compare-and-set on the active pointer.</summary>
    [Fact]
    public async Task ActivateAsync_WhenExpectedActiveVersionMatches_SwitchesThePointer()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var v1 = MemoryTestData.Document(owner, "v1");
        var v2 = MemoryTestData.Document(owner, "v2", "sha256:bb", v1.Id);
        _ = await WriteAsync(fixture, owner, v1, MemoryTestData.Chunks(v1), true, "k1");
        _ = await WriteAsync(fixture, owner, v2, MemoryTestData.Chunks(v2), false, "k2");

        var activated = await ActivateAsync(fixture, owner, v1.Id, "v2", "v1");

        activated.IsActivated.ShouldBeTrue();
        activated.PreviousActiveVersion.ShouldBe(v1.Version);
        (await ReadAsync(fixture, owner, v1.Id, null, false)).Record!.Version.ShouldBe(v2.Version);
        (await ReadAsync(fixture, owner, v1.Id, "v1", false)).State.ShouldBe(DocumentVersionState.Superseded);
    }

    /// <summary>Verifies activating with a stale expected active version is refused and changes nothing.</summary>
    [Fact]
    public async Task ActivateAsync_WhenExpectedActiveVersionIsStale_RejectsWithVersionConflict()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var v1 = MemoryTestData.Document(owner, "v1");
        var v2 = MemoryTestData.Document(owner, "v2", "sha256:bb", v1.Id);
        _ = await WriteAsync(fixture, owner, v1, MemoryTestData.Chunks(v1), true, "k1");
        _ = await WriteAsync(fixture, owner, v2, MemoryTestData.Chunks(v2), false, "k2");

        (await ActivateAsync(fixture, owner, v1.Id, "v2", "v0")).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.VersionConflict);
        (await ReadAsync(fixture, owner, v1.Id, null, false)).Record!.Version.ShouldBe(v1.Version);
    }

    /// <summary>Verifies activating an unknown version is reported as not found.</summary>
    [Fact]
    public async Task ActivateAsync_WhenVersionWasNeverStaged_ReportsNotFound()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner);
        _ = await WriteAsync(fixture, owner, record, MemoryTestData.Chunks(record), true, "k");

        (await ActivateAsync(fixture, owner, record.Id, "v9", record.Version.Value)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
    }

    /// <summary>Verifies activating the version that is already active replays.</summary>
    [Fact]
    public async Task ActivateAsync_WhenVersionIsAlreadyActive_Replays()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner);
        _ = await WriteAsync(fixture, owner, record, MemoryTestData.Chunks(record), true, "k");

        var replay = await ActivateAsync(fixture, owner, record.Id, record.Version.Value, record.Version.Value);

        replay.IsActivated.ShouldBeTrue();
        replay.Replayed.ShouldBeTrue();
    }

    /// <summary>Verifies a read of an unknown document reports not found.</summary>
    [Fact]
    public async Task ReadAsync_WhenDocumentDoesNotExist_ReportsNotFound()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();

        (await ReadAsync(fixture, owner, new DocumentId(Guid.NewGuid()), null, false)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
    }

    /// <summary>Verifies another tenant cannot observe a document and sees it as absent.</summary>
    [Fact]
    public async Task ReadAsync_WhenDocumentBelongsToAnotherTenant_ReportsNotFound()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner("tenant-a");
        var outsider = MemoryTestData.NewOwner("tenant-b");
        var record = MemoryTestData.Document(owner);
        _ = await WriteAsync(fixture, owner, record, MemoryTestData.Chunks(record), true, "k");

        (await ReadAsync(fixture, outsider, record.Id, null, true)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
    }

    /// <summary>Verifies another agent in the same tenant cannot observe a document.</summary>
    [Fact]
    public async Task ReadAsync_WhenDocumentBelongsToAnotherAgentInTheTenant_ReportsNotFound()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var other = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner, shared: true);
        _ = await WriteAsync(fixture, owner, record, MemoryTestData.Chunks(record), true, "k");

        (await ReadAsync(fixture, other, record.Id, null, true)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
    }

    /// <summary>Verifies private documents are invisible to another principal of the same agent and shared ones are visible.</summary>
    [Fact]
    public async Task ReadAsync_WhenAnotherPrincipalReadsTheSameAgentsDocument_HonoursTenantSharing()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var colleague = MemoryTestData.Owner(owner.AgentId, owner.SessionId, owner.RunId, "tenant", "colleague");
        var priv = MemoryTestData.Document(owner);
        var shared = MemoryTestData.Document(owner, shared: true);
        _ = await WriteAsync(fixture, owner, priv, MemoryTestData.Chunks(priv), true, "k1");
        _ = await WriteAsync(fixture, owner, shared, MemoryTestData.Chunks(shared), true, "k2");

        (await ReadAsync(fixture, colleague, priv.Id, null, false)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
        (await ReadAsync(fixture, colleague, shared.Id, null, false)).IsFound.ShouldBeTrue();
    }

    /// <summary>Verifies a write with a grant bound to a different chunk set is denied before anything is stored.</summary>
    [Fact]
    public async Task WriteAsync_WhenGrantBindsADifferentChunkSet_DeniesBeforeAnyWrite()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner);
        var forged = Factory(fixture).Write(record, MemoryTestData.Chunks(record, 2), true, owner.Authorization);
        var request = new DocumentWriteRequest(record, MemoryTestData.Chunks(record, 3), true, new IdempotencyKey("write-1"), forged.At, forged.Grant);

        var result = await fixture.Store.WriteAsync(request, TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Denied);
        (await ReadAsync(fixture, owner, record.Id, null, false)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
    }

    /// <summary>Verifies an unavailable grant store fails the operation closed.</summary>
    [Fact]
    public async Task WriteAsync_WhenGrantStoreIsUnavailable_DeniesAndWritesNothing()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner);
        var request = Factory(fixture).Write(record, MemoryTestData.Chunks(record), true, owner.Authorization);
        fixture.Grants.Fail = true;

        var result = await fixture.Store.WriteAsync(request, TestContext.Current.CancellationToken);
        fixture.Grants.Fail = false;

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Denied);
        (await ReadAsync(fixture, owner, record.Id, null, false)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
    }

    /// <summary>Verifies a consumed single-use grant cannot authorize a second write.</summary>
    [Fact]
    public async Task WriteAsync_WhenGrantWasAlreadyConsumed_Denies()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner);
        var request = Factory(fixture).Write(record, MemoryTestData.Chunks(record), true, owner.Authorization);
        _ = await fixture.Store.WriteAsync(request, TestContext.Current.CancellationToken);

        (await fixture.Store.WriteAsync(request, TestContext.Current.CancellationToken)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Denied);
    }

    /// <summary>Verifies a document claiming another agent than the authorized scope is refused.</summary>
    [Fact]
    public async Task WriteAsync_WhenRecordClaimsAnotherAgent_RejectsWithScopeMismatch()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var stranger = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(stranger);

        (await WriteAsync(fixture, owner, record, MemoryTestData.Chunks(record), true)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.ScopeMismatch);
    }

    /// <summary>Verifies a second agent cannot publish a version of another agent's document.</summary>
    [Fact]
    public async Task WriteAsync_WhenAnotherAgentPublishesAVersionOfTheDocument_RejectsWithScopeMismatch()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var stranger = MemoryTestData.NewOwner();
        var v1 = MemoryTestData.Document(owner);
        _ = await WriteAsync(fixture, owner, v1, MemoryTestData.Chunks(v1), true, "k1");
        var hijack = MemoryTestData.Document(stranger, "v2", "sha256:bb", v1.Id);

        (await WriteAsync(fixture, stranger, hijack, MemoryTestData.Chunks(hijack), true, "k2")).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.ScopeMismatch);
    }

    /// <summary>Verifies a tombstone hides the document immediately, clears the pointer, and names the chunks and pending store.</summary>
    [Fact]
    public async Task DeleteAsync_WhenTombstoning_HidesTheDocumentAndNamesChunksAndPendingStore()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner);
        var chunks = MemoryTestData.Chunks(record, 3);
        _ = await WriteAsync(fixture, owner, record, chunks, true);

        var deleted = await DeleteAsync(fixture, owner, record.Id, DocumentDeleteMode.Tombstone);

        deleted.Receipt!.LogicallyDeleted.ShouldBeTrue();
        deleted.Receipt.PhysicallyPurged.ShouldBeFalse();
        deleted.Receipt.ChunkIds.ShouldBe([.. chunks.Select(static chunk => chunk.Id)]);
        deleted.Receipt.PendingStores.ShouldBe([fixture.Store.Descriptor.Name]);
        deleted.Receipt.Generation.ShouldBeGreaterThan(0);
        var read = await ReadAsync(fixture, owner, record.Id, null, true);
        read.IsFound.ShouldBeFalse();
        read.Tombstone!.Id.ShouldBe(record.Id);
        _ = (await ReadAsync(fixture, owner, record.Id, record.Version.Value, true)).Tombstone.ShouldNotBeNull();
    }

    /// <summary>Verifies a purge removes stored chunks while the receipt keeps their identities for propagation, and repeating replays.</summary>
    [Fact]
    public async Task DeleteAsync_WhenPurgingAfterATombstoneAndRepeating_CompletesThenReplays()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner);
        var chunks = MemoryTestData.Chunks(record, 2);
        _ = await WriteAsync(fixture, owner, record, chunks, true);
        var tombstoned = await DeleteAsync(fixture, owner, record.Id, DocumentDeleteMode.Tombstone, "d1");

        var purged = await DeleteAsync(fixture, owner, record.Id, DocumentDeleteMode.Purge, "d2");
        var replay = await DeleteAsync(fixture, owner, record.Id, DocumentDeleteMode.Purge, "d3");

        purged.Replayed.ShouldBeFalse();
        purged.Receipt!.PhysicallyPurged.ShouldBeTrue();
        purged.Receipt.PendingStores.ShouldBeEmpty();
        purged.Receipt.ChunkIds.ShouldBe(tombstoned.Receipt!.ChunkIds);
        purged.Receipt.Generation.ShouldBe(tombstoned.Receipt.Generation);
        replay.Replayed.ShouldBeTrue();
        (await ReadAsync(fixture, owner, record.Id, null, true)).Tombstone!.PhysicallyPurged.ShouldBeTrue();
    }

    /// <summary>Verifies a deleted document cannot receive new versions or activations.</summary>
    [Fact]
    public async Task WriteAsync_WhenDocumentIsDeleted_RejectsWithInvalidTransition()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var v1 = MemoryTestData.Document(owner);
        _ = await WriteAsync(fixture, owner, v1, MemoryTestData.Chunks(v1), true, "k1");
        _ = await DeleteAsync(fixture, owner, v1.Id, DocumentDeleteMode.Tombstone);
        var v2 = MemoryTestData.Document(owner, "v2", "sha256:bb", v1.Id);

        (await WriteAsync(fixture, owner, v2, MemoryTestData.Chunks(v2), true, "k2")).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.InvalidTransition);
        (await ActivateAsync(fixture, owner, v1.Id, "v1", null)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.InvalidTransition);
    }

    /// <summary>Verifies another tenant cannot delete a document and sees it as absent.</summary>
    [Fact]
    public async Task DeleteAsync_WhenDocumentBelongsToAnotherTenant_ReportsNotFoundAndChangesNothing()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner("tenant-a");
        var outsider = MemoryTestData.NewOwner("tenant-b");
        var record = MemoryTestData.Document(owner);
        _ = await WriteAsync(fixture, owner, record, MemoryTestData.Chunks(record), true);

        (await DeleteAsync(fixture, outsider, record.Id, DocumentDeleteMode.Purge)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
        (await ReadAsync(fixture, owner, record.Id, null, false)).IsFound.ShouldBeTrue();
    }

    /// <summary>Verifies deletion with a grant bound to a different mode is denied before any change.</summary>
    [Fact]
    public async Task DeleteAsync_WhenGrantBindsADifferentMode_DeniesBeforeAnyChange()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner);
        _ = await WriteAsync(fixture, owner, record, MemoryTestData.Chunks(record), true);
        var forged = Factory(fixture).Delete(record.Id, DocumentDeleteMode.Tombstone, owner.Authorization);
        var request = new DocumentDeleteRequest(record.Id, DocumentDeleteMode.Purge, new IdempotencyKey("delete-1"), forged.At, forged.Grant);

        (await fixture.Store.DeleteAsync(request, TestContext.Current.CancellationToken)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Denied);
        (await ReadAsync(fixture, owner, record.Id, null, false)).IsFound.ShouldBeTrue();
    }

    /// <summary>Verifies a cancelled token stops a write before it commits.</summary>
    [Fact]
    public async Task WriteAsync_WhenCancelled_ThrowsWithoutWriting()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner);
        var request = Factory(fixture).Write(record, MemoryTestData.Chunks(record), true, owner.Authorization);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await fixture.Store.WriteAsync(request, cancelled.Token));

        (await ReadAsync(fixture, owner, record.Id, null, false)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
    }

    /// <summary>Verifies the descriptor names its audience and claims durability consistently with the fixture.</summary>
    [Fact]
    public void Descriptor_WhenRead_NamesItsAudienceAndClaimsDurabilityConsistently()
    {
        var fixture = new TFixture();

        fixture.Store.Descriptor.SecurityAudience.Value.ShouldNotBeNullOrWhiteSpace();
        fixture.Store.Descriptor.IsDurable.ShouldBe(fixture.Capabilities.SupportsDurability);
    }

    /// <summary>Verifies committed versions, the active pointer, and tombstones survive reopening a durable store.</summary>
    [Fact]
    public async Task ReopenAsync_WhenTheStoreIsDurable_RetainsVersionsPointerAndTombstones()
    {
        var fixture = new TFixture();
        if (!fixture.Capabilities.SupportsDurability)
        {
            return;
        }

        var owner = MemoryTestData.NewOwner();
        var v1 = MemoryTestData.Document(owner, "v1");
        var v2 = MemoryTestData.Document(owner, "v2", "sha256:bb", v1.Id);
        var gone = MemoryTestData.Document(owner);
        _ = await WriteAsync(fixture, owner, v1, MemoryTestData.Chunks(v1), true, "k1");
        _ = await WriteAsync(fixture, owner, v2, MemoryTestData.Chunks(v2, 3), true, "k2");
        _ = await WriteAsync(fixture, owner, gone, MemoryTestData.Chunks(gone), true, "k3");
        var generation = (await DeleteAsync(fixture, owner, gone.Id, DocumentDeleteMode.Tombstone)).Receipt!.Generation;

        var reopened = await fixture.ReopenAsync(TestContext.Current.CancellationToken);

        var active = await reopened.ReadAsync(Factory(fixture).Read(v1.Id, null, true, owner.Authorization), TestContext.Current.CancellationToken);
        active.Record!.Version.ShouldBe(v2.Version);
        active.Chunks.Length.ShouldBe(3);
        var old = await reopened.ReadAsync(Factory(fixture).Read(v1.Id, "v1", false, owner.Authorization), TestContext.Current.CancellationToken);
        old.State.ShouldBe(DocumentVersionState.Superseded);
        var tombstone = await reopened.ReadAsync(Factory(fixture).Read(gone.Id, null, false, owner.Authorization), TestContext.Current.CancellationToken);
        tombstone.Tombstone!.Generation.ShouldBe(generation);
        var replay = await reopened.WriteAsync(Factory(fixture).Write(v2, MemoryTestData.Chunks(v2, 3), true, owner.Authorization, "k2"), TestContext.Current.CancellationToken);
        replay.Replayed.ShouldBeTrue();
    }

    private static DocumentStoreRequestFactory Factory(TFixture fixture) => new(fixture.Grants, fixture.Store.Descriptor.SecurityAudience);

    private static async Task<DocumentWriteResult> WriteAsync(
        TFixture fixture,
        MemoryTestOwner owner,
        DocumentRecord record,
        ImmutableArray<DocumentChunk> chunks,
        bool activate,
        string key = "write-1") =>
        await fixture.Store.WriteAsync(Factory(fixture).Write(record, chunks, activate, owner.Authorization, key), TestContext.Current.CancellationToken);

    private static async Task<DocumentActivateResult> ActivateAsync(TFixture fixture, MemoryTestOwner owner, DocumentId id, string version, string? expectedActive, string key = "activate-1") =>
        await fixture.Store.ActivateAsync(Factory(fixture).Activate(id, version, expectedActive, owner.Authorization, key), TestContext.Current.CancellationToken);

    private static async Task<DocumentReadResult> ReadAsync(TFixture fixture, MemoryTestOwner owner, DocumentId id, string? version, bool includeChunks) =>
        await fixture.Store.ReadAsync(Factory(fixture).Read(id, version, includeChunks, owner.Authorization), TestContext.Current.CancellationToken);

    private static async Task<DocumentDeleteResult> DeleteAsync(TFixture fixture, MemoryTestOwner owner, DocumentId id, DocumentDeleteMode mode, string key = "delete-1") =>
        await fixture.Store.DeleteAsync(Factory(fixture).Delete(id, mode, owner.Authorization, key), TestContext.Current.CancellationToken);
}
