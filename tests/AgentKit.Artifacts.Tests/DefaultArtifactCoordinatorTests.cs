// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

public sealed class DefaultArtifactCoordinatorTests
{
    [Theory]
    [InlineData("profile-key", typeof(ArgumentNullException))]
    [InlineData("profile-version", typeof(ArgumentOutOfRangeException))]
    public void Constructor_WhenSelectedProfileIsInvalid_ThrowsExactParameter(
        string field, Type expectedExceptionType)
    {
        var options = new AgentArtifactOptions();
        if (field == "profile-key")
        {
            options.ProfileKey = default;
        }
        else
        {
            options.ProfileVersion = default;
        }

        void Construct() => _ = new DefaultArtifactCoordinator(
                new RecordingArtifactStore(), new RecordingSecurityAuthority(),
                new FixedIdentifierGenerator<SecurityRequestId>(ArtifactTestData.SecurityRequestId),
                new FixedIdentifierGenerator<ArtifactId>(ArtifactTestData.ArtifactId),
                new FixedIdentifierGenerator<ArtifactPreparationId>(ArtifactTestData.PreparationId),
                new FixedTimeProvider(), Options.Create(options));

        var exception = Should.Throw<ArgumentException>(Construct);
        exception.GetType().ShouldBe(expectedExceptionType);
        exception.ParamName.ShouldBe("options");
    }

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
    public async Task PrepareAsync_WhenDeclaredLengthUnderstatesActualContent_DeniesOnceObservedBytesExceedTheLimit()
    {
        // The declared length (4) passes the initial cheap check against the 4-byte limit, but the stream
        // actually yields more bytes than declared, so the bounded copy loop itself must detect the overrun.
        var store = new RecordingArtifactStore();
        var authority = new RecordingSecurityAuthority();
        var coordinator = CreateCoordinator(store, authority, maximumBytes: 4);
        var request = ArtifactTestData.CreatePrepare("0123456789"u8.ToArray(), declaredLength: 4);

        var result = await coordinator.PrepareAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.LimitExceeded);
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
    public async Task PrepareAsync_WhenOptionsMutateAcrossAuthorization_UsesCapturedAttempt()
    {
        var store = new RecordingArtifactStore();
        var authority = new GatedSecurityAuthority();
        var options = new AgentArtifactOptions
        {
            MaximumArtifactBytes = 1_024,
            CopyBufferBytes = 2,
            PreparationLifetime = TimeSpan.FromMinutes(5),
            ProfileKey = new ArtifactProfileKey("captured"),
            ProfileVersion = new ArtifactProfileVersion(7),
        };
        var coordinator = new DefaultArtifactCoordinator(
            store, authority, new FixedIdentifierGenerator<SecurityRequestId>(ArtifactTestData.SecurityRequestId),
            new FixedIdentifierGenerator<ArtifactId>(ArtifactTestData.ArtifactId),
            new FixedIdentifierGenerator<ArtifactPreparationId>(ArtifactTestData.PreparationId),
            new FixedTimeProvider(), Options.Create(options));
        var request = ArtifactTestData.CreatePrepare("content"u8.ToArray());

        var pending = coordinator.PrepareAsync(request, TestContext.Current.CancellationToken);
        var security = await authority.Observed.WaitAsync(TestContext.Current.CancellationToken);
        options.PreparationLifetime = TimeSpan.FromHours(9);
        options.ProfileKey = new ArtifactProfileKey("mutated");
        options.ProfileVersion = new ArtifactProfileVersion(99);
        authority.Release();
        _ = await pending;

        var dispatched = store.PrepareRequests.ShouldHaveSingleItem();
        dispatched.ProfileKey.ShouldBe(new ArtifactProfileKey("captured"));
        dispatched.ProfileVersion.ShouldBe(new ArtifactProfileVersion(7));
        dispatched.ExpiresAt.ShouldBe(ArtifactTestData.Now.AddMinutes(5));
        security.InputFingerprint.ShouldBe(ArtifactSecurityBinding.PrepareFingerprint(
            dispatched.ArtifactId, dispatched.PreparationId, dispatched.Version, dispatched.ProfileKey,
            dispatched.ProfileVersion, dispatched.TenantId, dispatched.CreatedBy, dispatched.DirectoryId,
            dispatched.Metadata, dispatched.CreatedAt, dispatched.ExpiresAt));
    }

    [Fact]
    public async Task FinalizeAsync_WhenAuthorized_DispatchesExactPreparationAndGrant()
    {
        var store = new RecordingArtifactStore();
        var authority = new RecordingSecurityAuthority();
        var coordinator = CreateCoordinator(store, authority);
        var request = new ArtifactFinalizeRequest(
            ArtifactTestData.PreparationId, ArtifactTestData.AgentId, ArtifactTestData.SessionId, null,
            ArtifactTestData.Correlation, ArtifactTestData.Identity, new IdempotencyKey("finalize-1"));

        _ = await coordinator.FinalizeAsync(request, TestContext.Current.CancellationToken);

        var security = authority.Requests.ShouldHaveSingleItem();
        security.Effect.ShouldBe(SecurityEffect.CreateOrReplace);
        security.Resources.ShouldBe([ArtifactSecurityBinding.PreparationResource(ArtifactTestData.PreparationId)]);
        security.InputFingerprint.ShouldBe(ArtifactSecurityBinding.FinalizeFingerprint(ArtifactTestData.PreparationId));
        var dispatched = store.FinalizeRequests.ShouldHaveSingleItem();
        dispatched.PreparationId.ShouldBe(ArtifactTestData.PreparationId);
        dispatched.Grant.ShouldBeSameAs(authority.IssuedGrants.ShouldHaveSingleItem());
    }

    [Fact]
    public async Task FinalizeAsync_WhenAuthorityDenies_DoesNotDispatchStoreEffect()
    {
        var store = new RecordingArtifactStore();
        var authority = new RecordingSecurityAuthority(allow: false);
        var coordinator = CreateCoordinator(store, authority);
        var request = new ArtifactFinalizeRequest(
            ArtifactTestData.PreparationId, ArtifactTestData.AgentId, ArtifactTestData.SessionId, null,
            ArtifactTestData.Correlation, ArtifactTestData.Identity, new IdempotencyKey("finalize-1"));

        var result = await coordinator.FinalizeAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactFinalizeRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        store.FinalizeRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task AbortAsync_WhenAuthorized_DispatchesExactPreparationAndGrant()
    {
        var store = new RecordingArtifactStore();
        var authority = new RecordingSecurityAuthority();
        var coordinator = CreateCoordinator(store, authority);
        var request = new ArtifactAbortRequest(
            ArtifactTestData.PreparationId, ArtifactTestData.AgentId, ArtifactTestData.SessionId,
            ArtifactTestData.Correlation, ArtifactTestData.Identity, ArtifactAbortReason.Cancelled, new IdempotencyKey("abort-1"));

        _ = await coordinator.AbortAsync(request, TestContext.Current.CancellationToken);

        var security = authority.Requests.ShouldHaveSingleItem();
        security.Effect.ShouldBe(SecurityEffect.Delete);
        security.ToolCallId.ShouldBeNull();
        security.Resources.ShouldBe([ArtifactSecurityBinding.PreparationResource(ArtifactTestData.PreparationId)]);
        security.InputFingerprint.ShouldBe(
            ArtifactSecurityBinding.AbortFingerprint(ArtifactTestData.PreparationId, ArtifactAbortReason.Cancelled));
        var dispatched = store.AbortRequests.ShouldHaveSingleItem();
        dispatched.PreparationId.ShouldBe(ArtifactTestData.PreparationId);
        dispatched.Grant.ShouldBeSameAs(authority.IssuedGrants.ShouldHaveSingleItem());
    }

    [Fact]
    public async Task AbortAsync_WhenAuthorityDenies_DoesNotDispatchStoreEffect()
    {
        var store = new RecordingArtifactStore();
        var authority = new RecordingSecurityAuthority(allow: false);
        var coordinator = CreateCoordinator(store, authority);
        var request = new ArtifactAbortRequest(
            ArtifactTestData.PreparationId, ArtifactTestData.AgentId, ArtifactTestData.SessionId,
            ArtifactTestData.Correlation, ArtifactTestData.Identity, ArtifactAbortReason.Abandoned, new IdempotencyKey("abort-1"));

        var result = await coordinator.AbortAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactAbortRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        store.AbortRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_WhenAuthorized_DispatchesExactReferenceAndGrant()
    {
        var store = new RecordingArtifactStore();
        var authority = new RecordingSecurityAuthority();
        var coordinator = CreateCoordinator(store, authority);
        var reference = ArtifactTestData.CreateReference();
        var request = new ArtifactDeleteRequest(
            ArtifactTestData.AgentId, ArtifactTestData.SessionId, null, ArtifactTestData.Correlation,
            ArtifactTestData.Identity, reference, new IdempotencyKey("delete-1"));

        _ = await coordinator.DeleteAsync(request, TestContext.Current.CancellationToken);

        var security = authority.Requests.ShouldHaveSingleItem();
        security.Effect.ShouldBe(SecurityEffect.Delete);
        security.Resources.ShouldBe([ArtifactSecurityBinding.ArtifactResource(reference.Id)]);
        security.InputFingerprint.ShouldBe(ArtifactSecurityBinding.DeleteFingerprint(reference));
        var dispatched = store.DeleteRequests.ShouldHaveSingleItem();
        dispatched.Reference.ShouldBeSameAs(reference);
        dispatched.Grant.ShouldBeSameAs(authority.IssuedGrants.ShouldHaveSingleItem());
    }

    [Fact]
    public async Task DeleteAsync_WhenAuthorityDenies_DoesNotDispatchStoreEffect()
    {
        var store = new RecordingArtifactStore();
        var authority = new RecordingSecurityAuthority(allow: false);
        var coordinator = CreateCoordinator(store, authority);
        var request = new ArtifactDeleteRequest(
            ArtifactTestData.AgentId, ArtifactTestData.SessionId, null, ArtifactTestData.Correlation,
            ArtifactTestData.Identity, ArtifactTestData.CreateReference(), new IdempotencyKey("delete-1"));

        var result = await coordinator.DeleteAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactDeleteRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        store.DeleteRequests.ShouldBeEmpty();
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
