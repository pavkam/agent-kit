// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Defines mandatory portable lifecycle, replay, authority, race, and tenant cases for <see cref="IArtifactStore"/>.</summary>
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
        var read = fixture.CreateRead(reference, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(read.Grant, TestContext.Current.CancellationToken);
        var content = await ReadTextAsync(store, read);
        var delete = fixture.CreateDelete(reference, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(delete.Grant, TestContext.Current.CancellationToken);
        var deleted = await store.DeleteAsync(delete, TestContext.Current.CancellationToken);
        var reread = fixture.CreateRead(reference, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(reread.Grant, TestContext.Current.CancellationToken);

        var missing = await store.ReadAsync(reread, TestContext.Current.CancellationToken);

        content.ShouldBe("portable content");
        deleted.ShouldBe(new ArtifactDeleted(false));
        missing.ShouldBeOfType<ArtifactReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
    }

    /// <summary>Verifies equivalent replay returns the original receipt despite regenerated request identities and instants.</summary>
    [Fact]
    public async Task PrepareAsync_WhenStableIntentReplays_ReturnsOriginalReceipt()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var first = fixture.CreatePrepare("same"u8.ToArray(), idempotencyKey: "replay");
        await fixture.RegisterGrantAsync(first.Grant, TestContext.Current.CancellationToken);
        var original = (await store.PrepareAsync(first, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactPrepared>();
        var retry = fixture.CreatePrepare(
            "same"u8.ToArray(), idempotencyKey: "replay", createdAt: first.CreatedAt.AddMinutes(1),
            lifetime: first.ExpiresAt - first.CreatedAt);
        await fixture.RegisterGrantAsync(retry.Grant, TestContext.Current.CancellationToken);

        var replay = await store.PrepareAsync(retry, TestContext.Current.CancellationToken);

        replay.ShouldBe(original);
    }

    /// <summary>Verifies a replay key cannot rebind a stable version or other preparation intent.</summary>
    [Theory]
    [InlineData("content")]
    [InlineData("version")]
    [InlineData("lifetime")]
    public async Task PrepareAsync_WhenStableIntentChanges_RejectsConflict(string changedField)
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var first = fixture.CreatePrepare("same"u8.ToArray(), idempotencyKey: "conflict");
        await fixture.RegisterGrantAsync(first.Grant, TestContext.Current.CancellationToken);
        _ = await store.PrepareAsync(first, TestContext.Current.CancellationToken);
        var changed = fixture.CreatePrepare(
            changedField == "content" ? "different"u8.ToArray() : "same"u8.ToArray(),
            idempotencyKey: "conflict",
            version: changedField == "version" ? new ArtifactVersion("2") : null,
            lifetime: changedField == "lifetime" ? TimeSpan.FromMinutes(6) : null);
        await fixture.RegisterGrantAsync(changed.Grant, TestContext.Current.CancellationToken);

        var result = await store.PrepareAsync(changed, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Conflict);
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
        await fixture.RegisterGrantAsync(denied.Grant, TestContext.Current.CancellationToken);
        var rejection = await store.PrepareAsync(denied, TestContext.Current.CancellationToken);
        var valid = fixture.CreatePrepare("authority"u8.ToArray(), artifactId: artifactId, preparationId: preparationId);
        await fixture.RegisterGrantAsync(valid.Grant, TestContext.Current.CancellationToken);

        var accepted = await store.PrepareAsync(valid, TestContext.Current.CancellationToken);

        rejection.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        _ = accepted.ShouldBeOfType<ArtifactPrepared>();
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
        var referenceA = await CommitAsync(
            fixture, store, "tenant a"u8.ToArray(), fixture.PrimaryIdentity, id, preparationId, version);
        var referenceB = await CommitAsync(
            fixture, store, "tenant b"u8.ToArray(), fixture.SecondaryIdentity, id, preparationId, version);
        var initialReadA = fixture.CreateRead(referenceA, fixture.PrimaryIdentity);
        var initialReadB = fixture.CreateRead(referenceB, fixture.SecondaryIdentity);
        await fixture.RegisterGrantAsync(initialReadA.Grant, TestContext.Current.CancellationToken);
        await fixture.RegisterGrantAsync(initialReadB.Grant, TestContext.Current.CancellationToken);
        var initialContentA = await ReadTextAsync(store, initialReadA);
        var initialContentB = await ReadTextAsync(store, initialReadB);
        var deleteA = fixture.CreateDelete(referenceA, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(deleteA.Grant, TestContext.Current.CancellationToken);
        _ = (await store.DeleteAsync(deleteA, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactDeleted>();
        var readA = fixture.CreateRead(referenceA, fixture.PrimaryIdentity);
        var readB = fixture.CreateRead(referenceB, fixture.SecondaryIdentity);
        await fixture.RegisterGrantAsync(readA.Grant, TestContext.Current.CancellationToken);
        await fixture.RegisterGrantAsync(readB.Grant, TestContext.Current.CancellationToken);

        var missingA = await store.ReadAsync(readA, TestContext.Current.CancellationToken);
        await using var openedB = (await store.ReadAsync(readB, TestContext.Current.CancellationToken))
            .ShouldBeOfType<ArtifactReadOpened>();
        using var readerB = new StreamReader(openedB.Content);

        initialContentA.ShouldBe("tenant a");
        initialContentB.ShouldBe("tenant b");
        referenceA.Id.ShouldBe(referenceB.Id);
        referenceA.Version.ShouldBe(referenceB.Version);
        referenceA.TenantId.ShouldNotBe(referenceB.TenantId);
        missingA.ShouldBeOfType<ArtifactReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        (await readerB.ReadToEndAsync(TestContext.Current.CancellationToken)).ShouldBe("tenant b");
    }

    /// <summary>Verifies competing finalize and abort operations establish exactly one terminal preparation outcome.</summary>
    [Fact]
    public async Task FinalizeAndAbortAsync_WhenTheyRace_ProduceOneTerminalOutcome()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepare = fixture.CreatePrepare("race"u8.ToArray());
        await fixture.RegisterGrantAsync(prepare.Grant, TestContext.Current.CancellationToken);
        _ = await store.PrepareAsync(prepare, TestContext.Current.CancellationToken);
        var finalize = fixture.CreateFinalize(prepare.PreparationId, prepare.Identity);
        var abort = fixture.CreateAbort(prepare.PreparationId, prepare.Identity);
        await fixture.RegisterGrantAsync(finalize.Grant, TestContext.Current.CancellationToken);
        await fixture.RegisterGrantAsync(abort.Grant, TestContext.Current.CancellationToken);

        var operations = await Task.WhenAll(
            Task.Run(async () => (object) await store.FinalizeAsync(finalize, TestContext.Current.CancellationToken)),
            Task.Run(async () => (object) await store.AbortAsync(abort, TestContext.Current.CancellationToken)));

        operations.Count(static result => result is ArtifactFinalized or ArtifactAborted).ShouldBe(1);
        if (operations.OfType<ArtifactFinalized>().Any())
        {
            operations.OfType<ArtifactAbortRejected>().Single().Failure.Kind.ShouldBe(ArtifactFailureKind.Conflict);
        }
        else
        {
            operations.OfType<ArtifactFinalizeRejected>().Single().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        }
    }

    /// <summary>Stages and publishes content with exact grants supplied by the fixture authority.</summary>
    private static async Task<ArtifactReference> CommitAsync(
        TFixture fixture,
        IArtifactStore store,
        byte[] content,
        ExecutionIdentity identity,
        ArtifactId? artifactId = null,
        ArtifactPreparationId? preparationId = null,
        ArtifactVersion? version = null)
    {
        var prepare = fixture.CreatePrepare(
            content, identity, artifactId: artifactId, preparationId: preparationId, version: version);
        await fixture.RegisterGrantAsync(prepare.Grant, TestContext.Current.CancellationToken);
        var prepared = (await store.PrepareAsync(prepare, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactPrepared>();
        var finalize = fixture.CreateFinalize(prepared.PreparationId, identity);
        await fixture.RegisterGrantAsync(finalize.Grant, TestContext.Current.CancellationToken);
        var reference = (await store.FinalizeAsync(finalize, TestContext.Current.CancellationToken))
            .ShouldBeOfType<ArtifactFinalized>().Reference;
        reference.Id.ShouldBe(prepared.ArtifactId);
        reference.Version.ShouldBe(prepare.Version);
        reference.Integrity.ContentHash.ShouldBe(prepare.Metadata.DeclaredContentHash);
        reference.TenantId.ShouldBe(identity.TenantId);
        return reference;
    }

    /// <summary>Reads and closes an owned artifact stream before a later lifecycle operation begins.</summary>
    private static async Task<string> ReadTextAsync(IArtifactStore store, ArtifactStoreReadRequest request)
    {
        await using var opened = (await store.ReadAsync(request, TestContext.Current.CancellationToken))
            .ShouldBeOfType<ArtifactReadOpened>();
        using var reader = new StreamReader(opened.Content);
        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }
}
