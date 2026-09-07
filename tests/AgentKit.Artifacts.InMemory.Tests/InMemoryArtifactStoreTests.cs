// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory.Tests;

public sealed class InMemoryArtifactStoreTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task FinalizeAsync_WhenPreparationExists_PublishesImmutableReference()
    {
        var fixture = new StoreFixture();
        var prepare = fixture.CreatePrepare("output"u8.ToArray());
        await fixture.RegisterPrepareGrantAsync(prepare);
        var prepared = (await fixture.Store.PrepareAsync(prepare, TestContext.Current.CancellationToken))
            .ShouldBeOfType<ArtifactPrepared>();
        var finalize = fixture.CreateFinalize(prepared.PreparationId, prepare.Identity);
        await fixture.RegisterFinalizeGrantAsync(finalize);

        var result = await fixture.Store.FinalizeAsync(finalize, TestContext.Current.CancellationToken);

        var reference = result.ShouldBeOfType<ArtifactFinalized>().Reference;
        reference.Id.ShouldBe(prepared.ArtifactId);
        reference.Integrity.ContentHash.ShouldBe(prepare.Metadata.DeclaredContentHash);
        reference.TenantId.ShouldBe(prepare.TenantId);
    }

    [Fact]
    public async Task PrepareAsync_WhenGrantFingerprintDiffers_DeniesBeforeStaging()
    {
        var fixture = new StoreFixture();
        var wrong = fixture.CreatePrepare("output"u8.ToArray(), grantFingerprint: new InputFingerprint("wrong"));
        await fixture.Grants.RegisterAsync(wrong.Grant, TestContext.Current.CancellationToken);

        var rejected = await fixture.Store.PrepareAsync(wrong, TestContext.Current.CancellationToken);
        var request = fixture.CreatePrepare("output"u8.ToArray());
        await fixture.RegisterPrepareGrantAsync(request);
        var accepted = await fixture.Store.PrepareAsync(request, TestContext.Current.CancellationToken);

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
        var otherIdentity = new ExecutionIdentity(
            new TenantId("other"), new PrincipalId("other-principal"), ExecutionSubjectKind.Human, ExtensionData.Empty);
        var foreignFinalize = fixture.CreateFinalize(prepare.PreparationId, otherIdentity);
        await fixture.RegisterFinalizeGrantAsync(foreignFinalize);

        var foreign = await fixture.Store.FinalizeAsync(foreignFinalize, TestContext.Current.CancellationToken);
        var ownerFinalize = fixture.CreateFinalize(prepare.PreparationId, prepare.Identity);
        await fixture.RegisterFinalizeGrantAsync(ownerFinalize);
        var owner = await fixture.Store.FinalizeAsync(ownerFinalize, TestContext.Current.CancellationToken);

        foreign.ShouldBeOfType<ArtifactFinalizeRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        _ = owner.ShouldBeOfType<ArtifactFinalized>();
    }

    [Fact]
    public async Task PrepareAsync_WhenIdempotencyKeyReplaysEquivalentContent_ReturnsOriginalReceipt()
    {
        var fixture = new StoreFixture();
        var first = fixture.CreatePrepare("output"u8.ToArray(), idempotencyKey: "same");
        await fixture.RegisterPrepareGrantAsync(first);
        var original = (await fixture.Store.PrepareAsync(first, TestContext.Current.CancellationToken))
            .ShouldBeOfType<ArtifactPrepared>();
        var retry = fixture.CreatePrepare("output"u8.ToArray(), idempotencyKey: "same");
        await fixture.RegisterPrepareGrantAsync(retry);

        var replay = await fixture.Store.PrepareAsync(retry, TestContext.Current.CancellationToken);

        replay.ShouldBe(original);
    }

    [Fact]
    public async Task PrepareAsync_WhenIdempotencyKeyChangesMeaning_RejectsConflict()
    {
        var fixture = new StoreFixture();
        var first = fixture.CreatePrepare("first"u8.ToArray(), idempotencyKey: "same");
        await fixture.RegisterPrepareGrantAsync(first);
        _ = await fixture.Store.PrepareAsync(first, TestContext.Current.CancellationToken);
        var conflicting = fixture.CreatePrepare("second"u8.ToArray(), idempotencyKey: "same");
        await fixture.RegisterPrepareGrantAsync(conflicting);

        var result = await fixture.Store.PrepareAsync(conflicting, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Conflict);
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
    public async Task FinalizeAsync_WhenConcurrentFreshGrantsRace_PublishesExactlyOneStableReference()
    {
        var fixture = new StoreFixture();
        var prepare = fixture.CreatePrepare("output"u8.ToArray());
        await fixture.RegisterPrepareGrantAsync(prepare);
        _ = await fixture.Store.PrepareAsync(prepare, TestContext.Current.CancellationToken);
        var requests = Enumerable.Range(0, 16)
            .Select(_ => fixture.CreateFinalize(prepare.PreparationId, prepare.Identity))
            .ToArray();
        foreach (var request in requests)
        {
            await fixture.RegisterFinalizeGrantAsync(request);
        }

        var results = await Task.WhenAll(requests.Select(request =>
            fixture.Store.FinalizeAsync(request, TestContext.Current.CancellationToken).AsTask()));

        results.ShouldAllBe(static result => result is ArtifactFinalized);
        results.Cast<ArtifactFinalized>().Select(static result => result.Reference).Distinct().Count().ShouldBe(1);
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
            return (await Store.FinalizeAsync(finalize, TestContext.Current.CancellationToken))
                .ShouldBeOfType<ArtifactFinalized>().Reference;
        }

        internal ArtifactStorePrepareRequest CreatePrepare(
            byte[] bytes,
            string idempotencyKey = "prepare",
            DateTimeOffset? expiresAt = null,
            InputFingerprint? grantFingerprint = null,
            bool legalHold = false)
        {
            var artifactId = new ArtifactId(NextGuid());
            var preparationId = new ArtifactPreparationId(NextGuid());
            var identity = Identity;
            var scope = CreateScope();
            var metadata = new ArtifactMetadata(
                new ArtifactOwnerId("session:owner"), "text/plain", bytes.LongLength,
                FileSecurityBinding.ContentFingerprint(bytes), ArtifactDataClassification.Internal,
                ArtifactOwnershipKind.Session, ArtifactMutability.Immutable,
                new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, legalHold));
            var grant = CreateGrant(
                scope, identity, SecurityEffect.Create,
                [ArtifactSecurityBinding.ArtifactResource(artifactId), ArtifactSecurityBinding.PreparationResource(preparationId)],
                grantFingerprint ?? ArtifactSecurityBinding.PrepareFingerprint(
                    artifactId, preparationId, new ArtifactDirectoryId("tool-output"), metadata));
            return new ArtifactStorePrepareRequest(
                artifactId, preparationId, new ArtifactVersion("1"), new ArtifactProfileKey("test"),
                new ArtifactProfileVersion(1), identity.TenantId, identity.PrincipalId,
                new ArtifactDirectoryId("tool-output"), metadata, [.. bytes], _now, expiresAt ?? _now.AddMinutes(5),
                scope, identity, grant, new IdempotencyKey(idempotencyKey));
        }

        internal ArtifactStoreFinalizeRequest CreateFinalize(ArtifactPreparationId preparationId, ExecutionIdentity identity)
        {
            var scope = CreateScope();
            var grant = CreateGrant(
                scope, identity, SecurityEffect.CreateOrReplace,
                [ArtifactSecurityBinding.PreparationResource(preparationId)],
                ArtifactSecurityBinding.FinalizeFingerprint(preparationId));
            return new ArtifactStoreFinalizeRequest(preparationId, scope, identity, grant, new IdempotencyKey($"finalize-{NextGuid()}"));
        }

        internal ArtifactStoreAbortRequest CreateAbort(ArtifactPreparationId preparationId, ExecutionIdentity identity)
        {
            var scope = CreateScope();
            var grant = CreateGrant(
                scope, identity, SecurityEffect.Delete,
                [ArtifactSecurityBinding.PreparationResource(preparationId)],
                ArtifactSecurityBinding.AbortFingerprint(preparationId, ArtifactAbortReason.Cancelled));
            return new ArtifactStoreAbortRequest(
                preparationId, ArtifactAbortReason.Cancelled, scope, identity, grant, new IdempotencyKey($"abort-{NextGuid()}"));
        }

        internal ArtifactStoreReadRequest CreateRead(ArtifactReference reference, ExecutionIdentity identity)
        {
            var scope = CreateScope();
            var grant = CreateGrant(
                scope, identity, SecurityEffect.Observe,
                [ArtifactSecurityBinding.ArtifactResource(reference.Id)],
                ArtifactSecurityBinding.ReadFingerprint(reference));
            return new ArtifactStoreReadRequest(reference, scope, identity, grant);
        }

        internal ArtifactStoreDeleteRequest CreateDelete(ArtifactReference reference, ExecutionIdentity identity)
        {
            var scope = CreateScope();
            var grant = CreateGrant(
                scope, identity, SecurityEffect.Delete,
                [ArtifactSecurityBinding.ArtifactResource(reference.Id)],
                ArtifactSecurityBinding.DeleteFingerprint(reference));
            return new ArtifactStoreDeleteRequest(reference, scope, identity, grant, new IdempotencyKey($"delete-{NextGuid()}"));
        }

        internal SecurityGrant CreateGrant(
            SecurityAuthorizationScope scope,
            ExecutionIdentity identity,
            SecurityEffect effect,
            ImmutableArray<ProtectedResource> resources,
            InputFingerprint fingerprint) => new(
                new GrantId(NextGuid()), new SecurityRequestId(NextGuid()), scope, identity, Store.SecurityAudience,
                SecurityOperationKind.Artifact, effect, resources, fingerprint, new SecurityPolicyVersion(1),
                new SecurityRevocationVersion(1), _now, _now.AddHours(1), 1);

        internal ValueTask RegisterPrepareGrantAsync(ArtifactStorePrepareRequest request) =>
            Grants.RegisterAsync(request.Grant, TestContext.Current.CancellationToken);

        internal ValueTask RegisterFinalizeGrantAsync(ArtifactStoreFinalizeRequest request) =>
            Grants.RegisterAsync(request.Grant, TestContext.Current.CancellationToken);

        internal ValueTask RegisterAbortGrantAsync(ArtifactStoreAbortRequest request) =>
            Grants.RegisterAsync(request.Grant, TestContext.Current.CancellationToken);

        internal ValueTask RegisterReadGrantAsync(ArtifactStoreReadRequest request) =>
            Grants.RegisterAsync(request.Grant, TestContext.Current.CancellationToken);

        internal ValueTask RegisterDeleteGrantAsync(ArtifactStoreDeleteRequest request) =>
            Grants.RegisterAsync(request.Grant, TestContext.Current.CancellationToken);

        private SecurityAuthorizationScope CreateScope() => new(
            new AgentId(NextGuid()), new SessionId(NextGuid()),
            new InRunOperationCorrelation(new OperationId(NextGuid()), new RunId(NextGuid()), null));

        private static ExecutionIdentity CreateIdentity(string tenant, string principal) => new(
            new TenantId(tenant), new PrincipalId(principal), ExecutionSubjectKind.Human, ExtensionData.Empty);

        private Guid NextGuid()
        {
            _sequence++;
            Span<byte> bytes = stackalloc byte[16];
            _ = BitConverter.TryWriteBytes(bytes, _sequence);
            return new Guid(bytes);
        }
    }
}
