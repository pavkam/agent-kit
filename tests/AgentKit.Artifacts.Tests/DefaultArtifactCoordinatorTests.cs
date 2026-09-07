// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

public sealed class DefaultArtifactCoordinatorTests
{
    [Fact]
    public async Task PrepareAsync_WhenContentIsValid_AuthorizesExactEffectAndDispatchesGrant()
    {
        var store = new RecordingArtifactStore();
        var authority = new RecordingSecurityAuthority();
        var coordinator = CreateCoordinator(store, authority);
        var request = ArtifactTestData.CreatePrepare("complete output"u8.ToArray());

        var result = await coordinator.PrepareAsync(request, TestContext.Current.CancellationToken);

        var prepared = result.ShouldBeOfType<ArtifactPrepared>();
        prepared.ArtifactId.ShouldBe(ArtifactTestData.ArtifactId);
        var security = authority.Requests.ShouldHaveSingleItem();
        security.Audience.ShouldBe(store.SecurityAudience);
        security.Kind.ShouldBe(SecurityOperationKind.Artifact);
        security.Effect.ShouldBe(SecurityEffect.Create);
        security.Resources.ShouldBe([
            ArtifactSecurityBinding.ArtifactResource(ArtifactTestData.ArtifactId),
            ArtifactSecurityBinding.PreparationResource(ArtifactTestData.PreparationId),
        ]);
        var dispatched = store.PrepareRequests.ShouldHaveSingleItem();
        dispatched.Grant.ShouldBeSameAs(authority.IssuedGrants.ShouldHaveSingleItem());
    }

    [Fact]
    public async Task PrepareAsync_WhenDeclaredLengthExceedsLimit_DeniesBeforeAuthorizationOrStore()
    {
        var store = new RecordingArtifactStore();
        var authority = new RecordingSecurityAuthority();
        var coordinator = CreateCoordinator(store, authority, maximumBytes: 4);
        var request = ArtifactTestData.CreatePrepare("five!"u8.ToArray());

        var result = await coordinator.PrepareAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.LimitExceeded);
        authority.Requests.ShouldBeEmpty();
        store.PrepareRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_WhenIntegrityDiffers_DeniesBeforeAuthorizationOrStore()
    {
        var store = new RecordingArtifactStore();
        var authority = new RecordingSecurityAuthority();
        var coordinator = CreateCoordinator(store, authority);
        var request = ArtifactTestData.CreatePrepare("content"u8.ToArray(), hash: new ContentHash("wrong"));

        var result = await coordinator.PrepareAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.IntegrityMismatch);
        authority.Requests.ShouldBeEmpty();
        store.PrepareRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_WhenAuthorityDenies_DoesNotDispatchStoreEffect()
    {
        var store = new RecordingArtifactStore();
        var authority = new RecordingSecurityAuthority(allow: false);
        var coordinator = CreateCoordinator(store, authority);

        var result = await coordinator.PrepareAsync(
            ArtifactTestData.CreatePrepare("content"u8.ToArray()), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        store.PrepareRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_WhenComplete_DoesNotDisposeCallerStream()
    {
        var store = new RecordingArtifactStore();
        var coordinator = CreateCoordinator(store, new RecordingSecurityAuthority());
        var request = ArtifactTestData.CreatePrepare("content"u8.ToArray());

        _ = await coordinator.PrepareAsync(request, TestContext.Current.CancellationToken);

        request.Content.CanRead.ShouldBeTrue();
    }

    [Fact]
    public async Task ReadAsync_WhenAuthorized_DispatchesExactReferenceAndGrant()
    {
        var store = new RecordingArtifactStore();
        var authority = new RecordingSecurityAuthority();
        var coordinator = CreateCoordinator(store, authority);
        var reference = ArtifactTestData.CreateReference();
        var request = new ArtifactReadRequest(
            ArtifactTestData.AgentId, ArtifactTestData.SessionId, null, ArtifactTestData.Correlation,
            ArtifactTestData.Identity, reference);

        _ = await coordinator.ReadAsync(request, TestContext.Current.CancellationToken);

        var security = authority.Requests.ShouldHaveSingleItem();
        security.Effect.ShouldBe(SecurityEffect.Observe);
        security.InputFingerprint.ShouldBe(ArtifactSecurityBinding.ReadFingerprint(reference));
        var dispatched = store.ReadRequests.ShouldHaveSingleItem();
        dispatched.Reference.ShouldBeSameAs(reference);
        dispatched.Grant.ShouldBeSameAs(authority.IssuedGrants.ShouldHaveSingleItem());
    }

    [Fact]
    public async Task DeleteAsync_WhenLegalHoldApplies_RejectsBeforeAuthorizationOrStore()
    {
        var store = new RecordingArtifactStore();
        var authority = new RecordingSecurityAuthority();
        var coordinator = CreateCoordinator(store, authority);
        var request = new ArtifactDeleteRequest(
            ArtifactTestData.AgentId, ArtifactTestData.SessionId, null, ArtifactTestData.Correlation,
            ArtifactTestData.Identity, ArtifactTestData.CreateReference(legalHold: true), new IdempotencyKey("delete-1"));

        var result = await coordinator.DeleteAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactDeleteRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.RetentionConflict);
        authority.Requests.ShouldBeEmpty();
        store.DeleteRequests.ShouldBeEmpty();
    }

    private static DefaultArtifactCoordinator CreateCoordinator(
        IArtifactStore store,
        ISecurityAuthority authority,
        long maximumBytes = 1_024) => new(
            store,
            authority,
            new FixedIdentifierGenerator<SecurityRequestId>(ArtifactTestData.SecurityRequestId),
            new FixedIdentifierGenerator<ArtifactId>(ArtifactTestData.ArtifactId),
            new FixedIdentifierGenerator<ArtifactPreparationId>(ArtifactTestData.PreparationId),
            new FixedTimeProvider(),
            Options.Create(new AgentArtifactOptions { MaximumArtifactBytes = maximumBytes, CopyBufferBytes = 2 }));
}
