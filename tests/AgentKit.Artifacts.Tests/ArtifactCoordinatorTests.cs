// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

/// <summary>Verifies the keyed coordinator's validation, authorization, routing, retention, event, and argument behavior end to end over in-memory stores.</summary>
public sealed partial class ArtifactCoordinatorTests
{
    private static readonly byte[] _content = "complete output"u8.ToArray();

    [Fact]
    public void Constructor_WhenADependencyIsInvalid_ThrowsNamingIt()
    {
        using var harness = CoordinatorHarness.Create();
        var snapshot = Snapshot();
        var options = AgentArtifactOptionsSnapshot.Create(new AgentArtifactOptions());
        var selector = new DefaultArtifactStoreSelector(harness.Services);
        var validator = new DefaultArtifactIntegrityValidator();
        var retention = new DefaultArtifactRetentionPolicy();
        var authorities = new FixedSecurityAuthoritySelector(harness.Authority);
        var events = new ArtifactEventDispatcher([], harness.Services);
        var clock = harness.Clock;
        var artifactIds = new SequentialIdentifierGenerator<ArtifactId>(static value => new ArtifactId(value), 1);
        var preparationIds = new SequentialIdentifierGenerator<ArtifactPreparationId>(static value => new ArtifactPreparationId(value), 2);
        var securityIds = new SequentialIdentifierGenerator<SecurityRequestId>(static value => new SecurityRequestId(value), 3);

        ArtifactCoordinator Build(
            ComponentKey<IArtifactCoordinator>? key = null,
            ArtifactProfileSnapshot? profile = null,
            ImmutableArray<ArtifactProfileSnapshot>? versions = null,
            IArtifactStoreSelector? stores = null,
            IArtifactIntegrityValidator? integrity = null,
            IArtifactRetentionPolicy? retentionPolicy = null,
            ISecurityAuthoritySelector? authoritySelector = null,
            IArtifactEventDispatcher? dispatcher = null,
            TimeProvider? time = null,
            IIdentifierGenerator<ArtifactId>? artifacts = null,
            IIdentifierGenerator<ArtifactPreparationId>? preparations = null,
            IIdentifierGenerator<SecurityRequestId>? security = null,
            AgentArtifactOptionsSnapshot? snapshotOptions = null) => new(
                key ?? ArtifactTestData.CoordinatorKey, profile ?? snapshot, versions ?? [snapshot], stores ?? selector,
                integrity ?? validator, retentionPolicy ?? retention, authoritySelector ?? authorities, dispatcher ?? events,
                time ?? clock, artifacts ?? artifactIds, preparations ?? preparationIds, security ?? securityIds, snapshotOptions ?? options);

        Should.Throw<ArgumentException>(() => Build(key: default(ComponentKey<IArtifactCoordinator>))).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => new ArtifactCoordinator(ArtifactTestData.CoordinatorKey, null!, [snapshot], selector, validator, retention, authorities, events, clock, artifactIds, preparationIds, securityIds, options)).ParamName.ShouldBe("profile");
        Should.Throw<ArgumentException>(() => Build(versions: default(ImmutableArray<ArtifactProfileSnapshot>))).ParamName.ShouldBe("profileVersions");
        Should.Throw<ArgumentNullException>(() => new ArtifactCoordinator(ArtifactTestData.CoordinatorKey, snapshot, [snapshot], null!, validator, retention, authorities, events, clock, artifactIds, preparationIds, securityIds, options)).ParamName.ShouldBe("stores");
        Should.Throw<ArgumentNullException>(() => new ArtifactCoordinator(ArtifactTestData.CoordinatorKey, snapshot, [snapshot], selector, null!, retention, authorities, events, clock, artifactIds, preparationIds, securityIds, options)).ParamName.ShouldBe("integrity");
        Should.Throw<ArgumentNullException>(() => new ArtifactCoordinator(ArtifactTestData.CoordinatorKey, snapshot, [snapshot], selector, validator, null!, authorities, events, clock, artifactIds, preparationIds, securityIds, options)).ParamName.ShouldBe("retention");
        Should.Throw<ArgumentNullException>(() => new ArtifactCoordinator(ArtifactTestData.CoordinatorKey, snapshot, [snapshot], selector, validator, retention, null!, events, clock, artifactIds, preparationIds, securityIds, options)).ParamName.ShouldBe("securityAuthorities");
        Should.Throw<ArgumentNullException>(() => new ArtifactCoordinator(ArtifactTestData.CoordinatorKey, snapshot, [snapshot], selector, validator, retention, authorities, null!, clock, artifactIds, preparationIds, securityIds, options)).ParamName.ShouldBe("events");
        Should.Throw<ArgumentNullException>(() => new ArtifactCoordinator(ArtifactTestData.CoordinatorKey, snapshot, [snapshot], selector, validator, retention, authorities, events, null!, artifactIds, preparationIds, securityIds, options)).ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new ArtifactCoordinator(ArtifactTestData.CoordinatorKey, snapshot, [snapshot], selector, validator, retention, authorities, events, clock, null!, preparationIds, securityIds, options)).ParamName.ShouldBe("artifactIds");
        Should.Throw<ArgumentNullException>(() => new ArtifactCoordinator(ArtifactTestData.CoordinatorKey, snapshot, [snapshot], selector, validator, retention, authorities, events, clock, artifactIds, null!, securityIds, options)).ParamName.ShouldBe("preparationIds");
        Should.Throw<ArgumentNullException>(() => new ArtifactCoordinator(ArtifactTestData.CoordinatorKey, snapshot, [snapshot], selector, validator, retention, authorities, events, clock, artifactIds, preparationIds, null!, options)).ParamName.ShouldBe("securityIds");
        Should.Throw<ArgumentNullException>(() => new ArtifactCoordinator(ArtifactTestData.CoordinatorKey, snapshot, [snapshot], selector, validator, retention, authorities, events, clock, artifactIds, preparationIds, securityIds, null!)).ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenRetainedRevisionsOmitTheCurrentProfile_ThrowsNamingTheRevisions()
    {
        using var harness = CoordinatorHarness.Create();
        var current = Snapshot(version: 2);
        var exception = Should.Throw<ArgumentException>(() => new ArtifactCoordinator(
            ArtifactTestData.CoordinatorKey, current, [Snapshot(version: 1)], new DefaultArtifactStoreSelector(harness.Services),
            new DefaultArtifactIntegrityValidator(), new DefaultArtifactRetentionPolicy(), new FixedSecurityAuthoritySelector(harness.Authority),
            new ArtifactEventDispatcher([], harness.Services), harness.Clock,
            new SequentialIdentifierGenerator<ArtifactId>(static value => new ArtifactId(value), 1),
            new SequentialIdentifierGenerator<ArtifactPreparationId>(static value => new ArtifactPreparationId(value), 2),
            new SequentialIdentifierGenerator<SecurityRequestId>(static value => new SecurityRequestId(value), 3),
            AgentArtifactOptionsSnapshot.Create(new AgentArtifactOptions())));

        exception.ParamName.ShouldBe("profileVersions");
    }

    [Fact]
    public async Task Operations_WhenRequestIsNull_ThrowArgumentNullExceptionBeforeAnyEffect()
    {
        using var harness = CoordinatorHarness.Create();
        var coordinator = harness.Coordinator;

        (await Should.ThrowAsync<ArgumentNullException>(async () => await coordinator.PrepareAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await coordinator.FinalizeAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await coordinator.AbortAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await coordinator.ReadAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await coordinator.DeleteAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await coordinator.ReconcileAsync(null!))).ParamName.ShouldBe("request");
        harness.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_WhenContentIsValid_AuthorizesExactEffectAndStagesThroughTheRoutedStore()
    {
        using var harness = CoordinatorHarness.Create();
        var request = ArtifactTestData.Prepare(_content);

        var prepared = (await harness.Coordinator.PrepareAsync(request, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactPrepared>();

        var security = harness.Authority.Requests.ShouldHaveSingleItem();
        security.Audience.ShouldBe(harness.Store.SecurityAudience);
        security.Kind.ShouldBe(SecurityOperationKind.Artifact);
        security.Effect.ShouldBe(SecurityEffect.Create);
        security.Authorization.ShouldBe(ArtifactTestData.Authorization);
        security.Resources.ShouldBe([
            ArtifactSecurityBinding.ArtifactResource(prepared.ArtifactId),
            ArtifactSecurityBinding.PreparationResource(prepared.PreparationId),
        ]);
        prepared.Version.ShouldBe(new ArtifactVersion("1"));
        prepared.ExpiresAt.ShouldBe(ArtifactTestData.Now.AddHours(1));
    }

    [Fact]
    public async Task PrepareAsync_WhenAuthorized_BindsTheResolvedRetentionAndObservedHashIntoTheGrant()
    {
        using var harness = CoordinatorHarness.Create(configureProfile: profile =>
            profile.DefaultRetention = new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), ArtifactTestData.Now.AddDays(30), false));
        var request = ArtifactTestData.Prepare(_content);

        var prepared = (await harness.Coordinator.PrepareAsync(request, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactPrepared>();

        var resolved = new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), ArtifactTestData.Now.AddDays(30), false);
        var expected = ArtifactSecurityBinding.PrepareFingerprint(
            prepared.ArtifactId, prepared.PreparationId, prepared.Version, ArtifactTestData.ProfileKey, new ArtifactProfileVersion(1),
            ArtifactTestData.Identity.TenantId, ArtifactTestData.Identity.PrincipalId, ArtifactTestData.Directory,
            new ArtifactMetadata(
                request.Metadata.OwnerId, request.Metadata.MediaType, request.Metadata.DeclaredLength, request.Metadata.DeclaredContentHash,
                request.Metadata.Classification, request.Metadata.Ownership, request.Metadata.Mutability, resolved, null),
            FileSecurityBinding.ContentFingerprint(_content), ArtifactTestData.Now, prepared.ExpiresAt);
        harness.Authority.Requests.ShouldHaveSingleItem().InputFingerprint.ShouldBe(expected);
    }

    [Fact]
    public async Task PrepareAsync_WhenComplete_DoesNotDisposeCallerStream()
    {
        using var harness = CoordinatorHarness.Create();
        var request = ArtifactTestData.Prepare(_content);

        _ = await harness.Coordinator.PrepareAsync(request, TestContext.Current.CancellationToken);

        request.Content.CanRead.ShouldBeTrue();
    }

    [Fact]
    public async Task PrepareAsync_WhenDeclaredLengthExceedsLimit_RejectsBeforeAuthorization()
    {
        using var harness = CoordinatorHarness.Create(options => options.MaximumArtifactBytes = 4);

        var result = await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare("five!"u8.ToArray()), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.LimitExceeded);
        harness.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_WhenDeclaredLengthUnderstatesContent_RejectsOnceObservedBytesExceedTheLimit()
    {
        using var harness = CoordinatorHarness.Create(options =>
        {
            options.MaximumArtifactBytes = 4;
            options.CopyBufferBytes = 2;
        });
        var content = "0123456789"u8.ToArray();

        var result = await harness.Coordinator.PrepareAsync(
            ArtifactTestData.Prepare(content, ArtifactTestData.Metadata(content, declaredLength: 4)), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.LimitExceeded);
        harness.Authority.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("length")]
    public async Task PrepareAsync_WhenIntegrityDiffers_RejectsBeforeAuthorization(string mismatch)
    {
        using var harness = CoordinatorHarness.Create();
        var metadata = mismatch == "hash"
            ? ArtifactTestData.Metadata(_content, hash: new ContentHash("sha256:wrong"))
            : ArtifactTestData.Metadata(_content, declaredLength: _content.Length - 1);

        var result = await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content, metadata), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.IntegrityMismatch);
        harness.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_WhenHashIsRequiredButNotDeclared_RejectsBeforeAuthorization()
    {
        using var harness = CoordinatorHarness.Create();

        var result = await harness.Coordinator.PrepareAsync(
            ArtifactTestData.Prepare(_content, ArtifactTestData.Metadata(_content, omitHash: true)), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.IntegrityMismatch);
        harness.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_WhenHashIsOptionalAndNotDeclared_RecordsTheObservedFingerprint()
    {
        using var harness = CoordinatorHarness.Create(options => options.RequireDeclaredContentHash = false);

        var reference = await harness.CommitAsync(_content, ArtifactTestData.Metadata(_content, omitHash: true));

        reference.Integrity.ContentHash.ShouldBe(FileSecurityBinding.ContentFingerprint(_content));
    }

    [Fact]
    public async Task PrepareAsync_WhenProfileDoesNotAdmitTheMutability_DeniesBeforeAuthorization()
    {
        using var harness = CoordinatorHarness.Create();

        var result = await harness.Coordinator.PrepareAsync(
            ArtifactTestData.Prepare(_content, ArtifactTestData.Metadata(_content, mutability: ArtifactMutability.AppendOnly)),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        harness.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_WhenProfileAdmitsTheMutability_StagesIt()
    {
        using var harness = CoordinatorHarness.Create(configureProfile: profile => _ = profile.AllowedMutability.Add(ArtifactMutability.AppendOnly));

        var reference = await harness.CommitAsync(_content, ArtifactTestData.Metadata(_content, mutability: ArtifactMutability.AppendOnly));

        reference.Mutability.ShouldBe(ArtifactMutability.AppendOnly);
    }

    [Fact]
    public async Task PrepareAsync_WhenProfileDoesNotAdmitExternalOwnership_DeniesBeforeAuthorization()
    {
        using var harness = CoordinatorHarness.Create(configureProfile: profile => _ = profile.AllowedMutability.Add(ArtifactMutability.ExternallyManaged));
        var external = new ExternalArtifactOwnership(new ExternalArtifactResourceId("vendor:1"), new Uri("https://files.example.com/1"), false);

        var result = await harness.Coordinator.PrepareAsync(
            ArtifactTestData.Prepare(_content, ArtifactTestData.Metadata(_content, external: external)), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        harness.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_WhenProfileAdmitsExternalOwnership_PreservesItOnTheReference()
    {
        using var harness = CoordinatorHarness.Create(configureProfile: profile =>
        {
            profile.AllowExternalOwnership = true;
            _ = profile.AllowedMutability.Add(ArtifactMutability.ExternallyManaged);
        });
        var external = new ExternalArtifactOwnership(new ExternalArtifactResourceId("vendor:1"), new Uri("https://files.example.com/1"), false);

        var reference = await harness.CommitAsync(_content, ArtifactTestData.Metadata(_content, external: external));

        reference.ExternalOwnership.ShouldBe(external);
        reference.Ownership.ShouldBe(ArtifactOwnershipKind.External);
    }

    [Fact]
    public async Task PrepareAsync_WhenRequestedRetentionAlreadyExpired_RejectsBeforeAuthorization()
    {
        using var harness = CoordinatorHarness.Create();
        var metadata = ArtifactTestData.Metadata(
            _content, retention: new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), ArtifactTestData.Now.AddSeconds(-1), false));

        var result = await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content, metadata), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.RetentionConflict);
        harness.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_WhenProfileDefaultsRetention_FinalizedReferenceCarriesTheResolvedDecision()
    {
        using var harness = CoordinatorHarness.Create(configureProfile: profile =>
            profile.DefaultRetention = new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), ArtifactTestData.Now.AddDays(7), true));

        var reference = await harness.CommitAsync(_content);

        reference.Retention.ShouldBe(new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), ArtifactTestData.Now.AddDays(7), true));
    }

    [Fact]
    public async Task PrepareAsync_WhenDirectoryIsNotRouted_RejectsAsUnavailableBeforeAuthorization()
    {
        using var harness = CoordinatorHarness.Create();

        var result = await harness.Coordinator.PrepareAsync(
            ArtifactTestData.Prepare(_content, directory: new ArtifactDirectoryId("unrouted")), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Unavailable);
        harness.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_WhenTheRoutedBackendIsNotRegistered_RejectsAsUnavailableAndNeverCreatesOne()
    {
        using var harness = CoordinatorHarness.Create(configureProfile: profile => profile.Routes[new ArtifactDirectoryId("ghost-dir")] = new ArtifactBackendKey("ghost"));

        var result = await harness.Coordinator.PrepareAsync(
            ArtifactTestData.Prepare(_content, directory: new ArtifactDirectoryId("ghost-dir")), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Unavailable);
        harness.Services.GetKeyedService<IArtifactStore>("ghost").ShouldBeNull();
        harness.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_WhenAuthorityDenies_DoesNotStageAnything()
    {
        using var harness = CoordinatorHarness.Create();
        harness.Authority.Deny = true;

        var result = await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        harness.Sink.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_WhenTheAuthoritySelectionIsUnavailable_DeniesFailClosed()
    {
        using var harness = CoordinatorHarness.Create(extra: services =>
            _ = services.AddSingleton<ISecurityAuthoritySelector>(new UnavailableAuthoritySelector()));

        var result = await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        harness.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_WhenOptionsAreMutatedAfterRegistration_UsesTheCapturedSnapshot()
    {
        AgentArtifactOptions? captured = null;
        using var harness = CoordinatorHarness.Create(options =>
        {
            captured = options;
            options.MaximumArtifactBytes = 4;
            options.PreparationLifetime = TimeSpan.FromMinutes(5);
        });
        captured!.MaximumArtifactBytes = 1_000;
        captured.PreparationLifetime = TimeSpan.FromHours(9);

        var rejected = await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content), TestContext.Current.CancellationToken);
        var accepted = await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare("ok"u8.ToArray(), key: "second"), TestContext.Current.CancellationToken);

        rejected.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.LimitExceeded);
        accepted.ShouldBeOfType<ArtifactPrepared>().ExpiresAt.ShouldBe(ArtifactTestData.Now.AddMinutes(5));
    }

    [Fact]
    public async Task PrepareAsync_WhenTheSameRequestReplays_ReturnsTheOriginalReceiptAndDoesNotExtendIt()
    {
        using var harness = CoordinatorHarness.Create();
        var first = (await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content), TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactPrepared>();
        harness.Clock.Advance(TimeSpan.FromMinutes(10));

        var replay = (await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content), TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactPrepared>();

        replay.ShouldBe(first);
    }

    [Fact]
    public async Task PrepareAsync_WhenReplayKeyChangesContent_RejectsWithConflict()
    {
        using var harness = CoordinatorHarness.Create();
        _ = await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content), TestContext.Current.CancellationToken);

        var result = await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare("different"u8.ToArray()), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactPrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Conflict);
    }

    [Fact]
    public async Task PrepareAsync_WhenTenantsReuseAReplayKey_StagesIndependently()
    {
        using var harness = CoordinatorHarness.Create();
        var first = await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content), TestContext.Current.CancellationToken);

        var second = await harness.Coordinator.PrepareAsync(
            ArtifactTestData.Prepare("other tenant"u8.ToArray(), authorization: ArtifactTestData.OtherAuthorization), TestContext.Current.CancellationToken);

        _ = first.ShouldBeOfType<ArtifactPrepared>();
        _ = second.ShouldBeOfType<ArtifactPrepared>();
    }

    [Fact]
    public async Task FinalizeAsync_WhenAuthorized_ReturnsTheImmutableReferenceCarryingTheCapturedProfile()
    {
        using var harness = CoordinatorHarness.Create();
        var request = ArtifactTestData.Prepare(_content);
        var prepared = (await harness.Coordinator.PrepareAsync(request, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactPrepared>();

        var finalized = (await harness.Coordinator.FinalizeAsync(ArtifactTestData.Finalize(prepared.PreparationId), TestContext.Current.CancellationToken))
            .ShouldBeOfType<ArtifactFinalized>();

        var reference = finalized.Reference;
        reference.Id.ShouldBe(prepared.ArtifactId);
        reference.Version.ShouldBe(prepared.Version);
        reference.DirectoryId.ShouldBe(ArtifactTestData.Directory);
        reference.ProfileKey.ShouldBe(ArtifactTestData.ProfileKey);
        reference.ProfileVersion.ShouldBe(new ArtifactProfileVersion(1));
        reference.TenantId.ShouldBe(ArtifactTestData.Identity.TenantId);
        reference.CreatedBy.ShouldBe(ArtifactTestData.Identity.PrincipalId);
        reference.OwnerId.ShouldBe(request.Metadata.OwnerId);
        reference.Length.ShouldBe(_content.LongLength);
        reference.Integrity.ContentHash.ShouldBe(FileSecurityBinding.ContentFingerprint(_content));
        reference.CreatedAt.ShouldBe(ArtifactTestData.Now);
        var security = harness.Authority.Requests.Last();
        security.Effect.ShouldBe(SecurityEffect.CreateOrReplace);
        security.Resources.ShouldBe([ArtifactSecurityBinding.PreparationResource(prepared.PreparationId)]);
        security.InputFingerprint.ShouldBe(ArtifactSecurityBinding.FinalizeFingerprint(prepared.PreparationId));
    }

    [Fact]
    public async Task FinalizeAsync_WhenRetried_ReturnsTheSameReference()
    {
        using var harness = CoordinatorHarness.Create();
        var prepared = (await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content), TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactPrepared>();
        var first = await harness.Coordinator.FinalizeAsync(ArtifactTestData.Finalize(prepared.PreparationId), TestContext.Current.CancellationToken);
        harness.Clock.Advance(TimeSpan.FromMinutes(1));

        var retry = await harness.Coordinator.FinalizeAsync(ArtifactTestData.Finalize(prepared.PreparationId, key: "finalize-retry"), TestContext.Current.CancellationToken);

        retry.ShouldBe(first);
    }

    [Fact]
    public async Task FinalizeAsync_WhenPreparationIsUnknownOrForeign_RejectsWithNotFound()
    {
        using var harness = CoordinatorHarness.Create();
        var prepared = (await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content), TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactPrepared>();

        var unknown = await harness.Coordinator.FinalizeAsync(ArtifactTestData.Finalize(new ArtifactPreparationId(Guid.Parse("99000000-0000-0000-0000-000000000099"))), TestContext.Current.CancellationToken);
        var foreign = await harness.Coordinator.FinalizeAsync(ArtifactTestData.Finalize(prepared.PreparationId, ArtifactTestData.OtherAuthorization), TestContext.Current.CancellationToken);

        unknown.ShouldBeOfType<ArtifactFinalizeRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        foreign.ShouldBeOfType<ArtifactFinalizeRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
    }

    [Fact]
    public async Task FinalizeAsync_WhenAuthorityDenies_LeavesThePreparationFinalizable()
    {
        using var harness = CoordinatorHarness.Create();
        var prepared = (await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content), TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactPrepared>();
        harness.Authority.Deny = true;

        var denied = await harness.Coordinator.FinalizeAsync(ArtifactTestData.Finalize(prepared.PreparationId), TestContext.Current.CancellationToken);
        harness.Authority.Deny = false;
        var accepted = await harness.Coordinator.FinalizeAsync(ArtifactTestData.Finalize(prepared.PreparationId, key: "again"), TestContext.Current.CancellationToken);

        denied.ShouldBeOfType<ArtifactFinalizeRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        _ = accepted.ShouldBeOfType<ArtifactFinalized>();
    }

    [Fact]
    public async Task AbortAsync_WhenPrepared_RemovesStagingAndReplaysAsAlreadyAbsent()
    {
        using var harness = CoordinatorHarness.Create();
        var prepared = (await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content), TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactPrepared>();

        var first = await harness.Coordinator.AbortAsync(ArtifactTestData.Abort(prepared.PreparationId), TestContext.Current.CancellationToken);
        var second = await harness.Coordinator.AbortAsync(ArtifactTestData.Abort(prepared.PreparationId, key: "abort-2"), TestContext.Current.CancellationToken);
        var finalize = await harness.Coordinator.FinalizeAsync(ArtifactTestData.Finalize(prepared.PreparationId), TestContext.Current.CancellationToken);

        first.ShouldBe(new ArtifactAborted(false));
        second.ShouldBe(new ArtifactAborted(true));
        finalize.ShouldBeOfType<ArtifactFinalizeRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        var security = harness.Authority.Requests.First(static request => request.Effect == SecurityEffect.Delete);
        security.ToolCallId.ShouldBeNull();
        security.Resources.ShouldBe([ArtifactSecurityBinding.PreparationResource(prepared.PreparationId)]);
    }

    [Fact]
    public async Task AbortAsync_WhenUnknownCommittedOrDenied_ReturnsTypedRejections()
    {
        using var harness = CoordinatorHarness.Create();
        var prepare = ArtifactTestData.Prepare(_content);
        var prepared = (await harness.Coordinator.PrepareAsync(prepare, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactPrepared>();
        _ = await harness.Coordinator.FinalizeAsync(ArtifactTestData.Finalize(prepared.PreparationId), TestContext.Current.CancellationToken);

        var unknown = await harness.Coordinator.AbortAsync(ArtifactTestData.Abort(new ArtifactPreparationId(Guid.Parse("99000000-0000-0000-0000-000000000099"))), TestContext.Current.CancellationToken);
        var committed = await harness.Coordinator.AbortAsync(ArtifactTestData.Abort(prepared.PreparationId, key: "abort-2"), TestContext.Current.CancellationToken);
        harness.Authority.Deny = true;
        var denied = await harness.Coordinator.AbortAsync(ArtifactTestData.Abort(prepared.PreparationId, key: "abort-3"), TestContext.Current.CancellationToken);

        unknown.ShouldBeOfType<ArtifactAbortRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        committed.ShouldBeOfType<ArtifactAbortRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Conflict);
        denied.ShouldBeOfType<ArtifactAbortRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
    }

    [Fact]
    public async Task ReadAsync_WhenCommitted_ReturnsTheCompleteOwnedContentAfterAuthorizingTheExactReference()
    {
        using var harness = CoordinatorHarness.Create();
        var reference = await harness.CommitAsync(_content);

        var text = await harness.ReadTextAsync(reference);

        text.ShouldBe("complete output");
        var security = harness.Authority.Requests.Last();
        security.Effect.ShouldBe(SecurityEffect.Observe);
        security.InputFingerprint.ShouldBe(ArtifactSecurityBinding.ReadFingerprint(reference));
        security.Resources.ShouldBe([ArtifactSecurityBinding.ArtifactResource(reference.Id)]);
    }

    [Fact]
    public async Task ReadAsync_WhenForeignTenantOrDeniedOrTombstoned_RejectsWithTypedFailures()
    {
        using var harness = CoordinatorHarness.Create();
        var reference = await harness.CommitAsync(_content);

        var foreign = await harness.Coordinator.ReadAsync(ArtifactTestData.Read(reference, ArtifactTestData.OtherAuthorization), TestContext.Current.CancellationToken);
        harness.Authority.Deny = true;
        var denied = await harness.Coordinator.ReadAsync(ArtifactTestData.Read(reference), TestContext.Current.CancellationToken);
        harness.Authority.Deny = false;
        _ = await harness.Coordinator.DeleteAsync(ArtifactTestData.Delete(reference), TestContext.Current.CancellationToken);
        var tombstoned = await harness.Coordinator.ReadAsync(ArtifactTestData.Read(reference), TestContext.Current.CancellationToken);

        foreign.ShouldBeOfType<ArtifactReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        denied.ShouldBeOfType<ArtifactReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        tombstoned.ShouldBeOfType<ArtifactReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
    }

    [Fact]
    public async Task ReadAsync_WhenReferenceBelongsToAnotherProfileOrAnUnretainedRevision_RejectsWithoutAuthorization()
    {
        using var harness = CoordinatorHarness.Create();
        var reference = await harness.CommitAsync(_content);
        var authorizationsBefore = harness.Authority.Requests.Count;

        var otherProfile = await harness.Coordinator.ReadAsync(ArtifactTestData.Read(Rebind(reference, profileKey: new ArtifactProfileKey("other"))), TestContext.Current.CancellationToken);
        var unretained = await harness.Coordinator.ReadAsync(ArtifactTestData.Read(Rebind(reference, profileVersion: new ArtifactProfileVersion(9))), TestContext.Current.CancellationToken);
        var unrouted = await harness.Coordinator.ReadAsync(ArtifactTestData.Read(Rebind(reference, directory: new ArtifactDirectoryId("unrouted"))), TestContext.Current.CancellationToken);

        otherProfile.ShouldBeOfType<ArtifactReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        unretained.ShouldBeOfType<ArtifactReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Unavailable);
        unrouted.ShouldBeOfType<ArtifactReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Unavailable);
        harness.Authority.Requests.Count.ShouldBe(authorizationsBefore);
    }

    [Fact]
    public async Task DeleteAsync_WhenCommitted_TombstonesAndReplaysAsAlreadyAbsent()
    {
        using var harness = CoordinatorHarness.Create();
        var reference = await harness.CommitAsync(_content);

        var first = await harness.Coordinator.DeleteAsync(ArtifactTestData.Delete(reference), TestContext.Current.CancellationToken);
        var replay = await harness.Coordinator.DeleteAsync(ArtifactTestData.Delete(reference, key: "delete-2"), TestContext.Current.CancellationToken);

        first.ShouldBe(new ArtifactDeleted(false));
        replay.ShouldBe(new ArtifactDeleted(true));
        var security = harness.Authority.Requests.First(request => request.InputFingerprint == ArtifactSecurityBinding.DeleteFingerprint(reference));
        security.Effect.ShouldBe(SecurityEffect.Delete);
        security.Resources.ShouldBe([ArtifactSecurityBinding.ArtifactResource(reference.Id)]);
    }

    [Fact]
    public async Task DeleteAsync_WhenLegalHoldApplies_RejectsBeforeAuthorizationAndKeepsTheContent()
    {
        using var harness = CoordinatorHarness.Create();
        var held = ArtifactTestData.Metadata(_content, retention: new ArtifactRetention(new ArtifactRetentionPolicyKey("hold"), null, true));
        var reference = await harness.CommitAsync(_content, held);
        var authorizationsBefore = harness.Authority.Requests.Count;

        var result = await harness.Coordinator.DeleteAsync(ArtifactTestData.Delete(reference), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactDeleteRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.RetentionConflict);
        harness.Authority.Requests.Count.ShouldBe(authorizationsBefore);
        (await harness.ReadTextAsync(reference)).ShouldBe("complete output");
    }

    [Fact]
    public async Task DeleteAsync_WhenTheExternalOwnerKeepsDeleteAuthority_RejectsBeforeAuthorizationAndKeepsTheContent()
    {
        using var harness = CoordinatorHarness.Create(configureProfile: profile =>
        {
            profile.AllowExternalOwnership = true;
            _ = profile.AllowedMutability.Add(ArtifactMutability.ExternallyManaged);
        });
        var external = new ExternalArtifactOwnership(new ExternalArtifactResourceId("vendor:1"), new Uri("https://files.example.com/1"), false);
        var reference = await harness.CommitAsync(_content, ArtifactTestData.Metadata(_content, external: external));
        var authorizationsBefore = harness.Authority.Requests.Count;

        var result = await harness.Coordinator.DeleteAsync(ArtifactTestData.Delete(reference), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactDeleteRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.RetentionConflict);
        harness.Authority.Requests.Count.ShouldBe(authorizationsBefore);
        (await harness.ReadTextAsync(reference)).ShouldBe("complete output");
    }

    [Fact]
    public async Task DeleteAsync_WhenAuthorityDeniesOrTheTenantIsForeign_DoesNotDelete()
    {
        using var harness = CoordinatorHarness.Create();
        var reference = await harness.CommitAsync(_content);
        harness.Authority.Deny = true;

        var denied = await harness.Coordinator.DeleteAsync(ArtifactTestData.Delete(reference), TestContext.Current.CancellationToken);
        harness.Authority.Deny = false;
        var foreign = await harness.Coordinator.DeleteAsync(ArtifactTestData.Delete(reference, ArtifactTestData.OtherAuthorization), TestContext.Current.CancellationToken);

        denied.ShouldBeOfType<ArtifactDeleteRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        foreign.ShouldBeOfType<ArtifactDeleteRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        (await harness.ReadTextAsync(reference)).ShouldBe("complete output");
    }

    [Fact]
    public async Task FinalizeAsync_WhenPreparationLivesOnASecondBackend_ProbesEachBackendInOrderUntilOneAnswers()
    {
        using var harness = CoordinatorHarness.Create(
            configureProfile: profile => profile.Routes[new ArtifactDirectoryId("second")] = new ArtifactBackendKey("backend-b"),
            extra: services => _ = services.AddInMemoryArtifactStore(new ArtifactBackendKey("backend-b")));

        var prepared = (await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content, directory: new ArtifactDirectoryId("second")), TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactPrepared>();
        var finalized = (await harness.Coordinator.FinalizeAsync(ArtifactTestData.Finalize(prepared.PreparationId), TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactFinalized>();
        var text = await harness.ReadTextAsync(finalized.Reference);

        finalized.Reference.DirectoryId.ShouldBe(new ArtifactDirectoryId("second"));
        text.ShouldBe("complete output");
        harness.Authority.Requests.Count(static request => request.Effect == SecurityEffect.CreateOrReplace).ShouldBe(2);
    }

    [Fact]
    public async Task FinalizeAsync_WhenABackendCannotBeConsultedAndNoneAnswers_RejectsAsUnavailable()
    {
        using var harness = CoordinatorHarness.Create(configureProfile: profile => profile.Routes[new ArtifactDirectoryId("ghost-dir")] = new ArtifactBackendKey("ghost"));

        var result = await harness.Coordinator.FinalizeAsync(
            ArtifactTestData.Finalize(new ArtifactPreparationId(Guid.Parse("99000000-0000-0000-0000-000000000099"))), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactFinalizeRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Unavailable);
    }

    [Fact]
    public async Task ReadAsync_WhenTheRouteChangedInANewProfileRevision_StillResolvesReferencesCreatedUnderTheOldOne()
    {
        var clock = new FakeTimeProvider(ArtifactTestData.Now);
        var grants = new Permissions.InMemory.InMemorySecurityGrantStore(clock);
        var sharedStore = new InMemoryArtifactStore(grants, clock);
        ArtifactReference reference;
        using (var first = CoordinatorHarness.Create(extra: services => _ = services.AddKeyedSingleton<IArtifactStore>(ArtifactTestData.BackendKey.Value, sharedStore), clock: clock, grants: grants))
        {
            reference = await first.CommitAsync(_content);
        }

        var replacement = new InMemoryArtifactStore(grants, clock);
        using var second = CoordinatorHarness.Create(
            configureProfile: profile =>
            {
                profile.Version = new ArtifactProfileVersion(2);
                profile.Routes[ArtifactTestData.Directory] = new ArtifactBackendKey("backend-v2");
            },
            extra: services =>
            {
                _ = services.AddKeyedSingleton<IArtifactStore>(ArtifactTestData.BackendKey.Value, sharedStore);
                _ = services.AddKeyedSingleton<IArtifactStore>("backend-v2", replacement);
                _ = services.AddArtifactProfile(ArtifactTestData.ProfileKey, profile =>
                {
                    profile.DefaultDirectory = ArtifactTestData.Directory;
                    profile.Routes[ArtifactTestData.Directory] = ArtifactTestData.BackendKey;
                });
            },
            clock: clock,
            grants: grants);

        var oldText = await second.ReadTextAsync(reference);
        var fresh = await second.CommitAsync("new route"u8.ToArray(), key: "fresh");

        oldText.ShouldBe("complete output");
        reference.ProfileVersion.ShouldBe(new ArtifactProfileVersion(1));
        fresh.ProfileVersion.ShouldBe(new ArtifactProfileVersion(2));
        (await second.ReadTextAsync(fresh)).ShouldBe("new route");
    }

    [Fact]
    public async Task Events_WhenSinkFailsOrIsCancelled_DoNotChangeTheLifecycleOutcome()
    {
        using var harness = CoordinatorHarness.Create();
        harness.Sink.Failure = new InvalidOperationException("sink down");

        var reference = await harness.CommitAsync(_content);

        reference.Length.ShouldBe(_content.LongLength);
        _ = harness.Sink.Events.OfType<ArtifactPreparedEvent>().ShouldHaveSingleItem();
        _ = harness.Sink.Events.OfType<ArtifactFinalizedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Events_WhenLifecycleCompletes_PublishOneContentFreeEventPerTransitionInOrder()
    {
        using var harness = CoordinatorHarness.Create();
        var reference = await harness.CommitAsync(_content);
        _ = await harness.Coordinator.DeleteAsync(ArtifactTestData.Delete(reference), TestContext.Current.CancellationToken);

        var prepared = harness.Sink.Events[0].ShouldBeOfType<ArtifactPreparedEvent>();
        var finalized = harness.Sink.Events[1].ShouldBeOfType<ArtifactFinalizedEvent>();
        var deleted = harness.Sink.Events[2].ShouldBeOfType<ArtifactDeletedEvent>();

        harness.Sink.Events.Count.ShouldBe(3);
        prepared.ArtifactId.ShouldBe(reference.Id);
        finalized.ArtifactId.ShouldBe(reference.Id);
        finalized.PreparationId.ShouldBe(prepared.PreparationId);
        deleted.AlreadyAbsent.ShouldBeFalse();
        harness.Sink.Events.ShouldAllBe(artifactEvent => artifactEvent.CoordinatorKey == ArtifactTestData.CoordinatorKey
            && artifactEvent.TenantId == ArtifactTestData.Identity.TenantId && artifactEvent.ProfileKey == ArtifactTestData.ProfileKey);
    }

    [Fact]
    public async Task AbortAsync_WhenStagingIsRemoved_PublishesAnAbortedEvent()
    {
        using var harness = CoordinatorHarness.Create();
        var prepared = (await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content), TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactPrepared>();

        _ = await harness.Coordinator.AbortAsync(ArtifactTestData.Abort(prepared.PreparationId), TestContext.Current.CancellationToken);

        var aborted = harness.Sink.Events.OfType<ArtifactAbortedEvent>().ShouldHaveSingleItem();
        aborted.PreparationId.ShouldBe(prepared.PreparationId);
        aborted.Reason.ShouldBe(ArtifactAbortReason.Cancelled);
        aborted.AlreadyAbsent.ShouldBeFalse();
    }

    [Fact]
    public async Task Operations_WhenCancelled_PropagateCancellationWithoutPublishingEvents()
    {
        using var harness = CoordinatorHarness.Create();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content), cancelled.Token));

        harness.Sink.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReplaceArtifactCoordinator_WhenAReplacementIsRegistered_ResolvesItUnderTheSameKey()
    {
        using var harness = CoordinatorHarness.Create(extra: services =>
            _ = services.ReplaceArtifactCoordinator<ThrowingCoordinator>(ArtifactTestData.CoordinatorKey));

        _ = await Should.ThrowAsync<NotSupportedException>(async () => await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content)));
    }

    private static ArtifactProfileSnapshot Snapshot(long version = 1) => ArtifactProfileSnapshot.Create(
        ArtifactTestData.ProfileKey,
        new ArtifactProfileOptions
        {
            Version = new ArtifactProfileVersion(version),
            DefaultDirectory = ArtifactTestData.Directory,
            Routes = { [ArtifactTestData.Directory] = ArtifactTestData.BackendKey },
        });

    private static ArtifactReference Rebind(
        ArtifactReference reference,
        ArtifactProfileKey? profileKey = null,
        ArtifactProfileVersion? profileVersion = null,
        ArtifactDirectoryId? directory = null) => new(
            reference.Id, reference.Version, directory ?? reference.DirectoryId, profileKey ?? reference.ProfileKey,
            profileVersion ?? reference.ProfileVersion, reference.TenantId, reference.OwnerId, reference.CreatedBy,
            reference.MediaType, reference.Length, reference.Integrity, reference.Classification, reference.Ownership,
            reference.Mutability, reference.Retention, reference.ExternalOwnership, reference.CreatedAt);

    private sealed class UnavailableAuthoritySelector: ISecurityAuthoritySelector
    {
        public ValueTask<SecurityAuthoritySelectionResult> SelectAsync(SecurityAuthorizationContext authorization, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SecurityAuthoritySelectionResult>(new SecurityAuthoritySelectionUnavailable(authorization, "No authority."));
    }

    private sealed class ThrowingCoordinator: IArtifactCoordinator
    {
        public Task<ArtifactPrepareResult> PrepareAsync(ArtifactPrepareRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<ArtifactFinalizeResult> FinalizeAsync(ArtifactFinalizeRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<ArtifactAbortResult> AbortAsync(ArtifactAbortRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ArtifactReadResult> ReadAsync(ArtifactReadRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<ArtifactDeleteResult> DeleteAsync(ArtifactDeleteRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<ArtifactReconciliationResult> ReconcileAsync(ArtifactReconciliationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
