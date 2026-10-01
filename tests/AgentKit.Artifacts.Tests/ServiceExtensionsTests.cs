// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

/// <summary>Verifies artifact registration, keyed selection, idempotency, conflicts, and replacement without a hidden backend.</summary>
public sealed class ServiceExtensionsTests
{
    private static readonly ComponentKey<IArtifactCoordinator> _key = new("one");
    private static readonly ArtifactProfileKey _profile = new("profile");

    [Fact]
    public void Registrations_WhenServicesIsNull_ThrowExactArgumentNullException()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddAgentArtifacts(_key, _profile)).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => services.AddArtifactProfile(_profile, NoProfile)).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => services.ReplaceArtifactProfile(_profile, NoProfile)).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => services.AddArtifactStore<InMemoryArtifactStore>(new ArtifactBackendKey("b"))).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => services.ReplaceArtifactStore<InMemoryArtifactStore>(new ArtifactBackendKey("b"))).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => services.AddArtifactEventSink<RecordingArtifactEventSink>(_key, Sink("s"))).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => services.ReplaceArtifactCoordinator<InMemoryCoordinator>(_key)).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => _ = services.ReplaceArtifactIntegrityValidator<DefaultArtifactIntegrityValidator>()).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => _ = services.ReplaceArtifactRetentionPolicy<DefaultArtifactRetentionPolicy>()).ParamName.ShouldBe("services");
    }

    [Fact]
    public void Registrations_WhenKeysAreBlankOrArgumentsNull_ThrowNamingThem()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddAgentArtifacts(default, _profile)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentException>(() => services.AddAgentArtifacts(_key, default)).ParamName.ShouldBe("profileKey");
        Should.Throw<ArgumentException>(() => services.AddArtifactProfile(default, NoProfile)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => services.AddArtifactProfile(_profile, null!)).ParamName.ShouldBe("configure");
        Should.Throw<ArgumentException>(() => services.AddArtifactStore<InMemoryArtifactStore>(default)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentException>(() => services.AddArtifactEventSink<RecordingArtifactEventSink>(default, Sink("s"))).ParamName.ShouldBe("coordinatorKey");
        Should.Throw<ArgumentNullException>(() => services.AddArtifactEventSink<RecordingArtifactEventSink>(_key, null!)).ParamName.ShouldBe("registration");
        Should.Throw<ArgumentException>(() => services.ReplaceArtifactCoordinator<InMemoryCoordinator>(default)).ParamName.ShouldBe("key");
    }

    [Fact]
    public void AddAgentArtifacts_WhenMechanicsAreInvalid_FailsAtRegistration()
    {
        var services = new ServiceCollection();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => services.AddAgentArtifacts(_key, _profile, static options => options.MaximumArtifactBytes = 0));
    }

    [Fact]
    public void AddAgentArtifacts_WhenRegistered_ResolvesKeyedCoordinatorAndDefaultsButNoStoreOrProfile()
    {
        var services = Composition();
        _ = services.AddAgentArtifacts(_key, _profile);
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredKeyedService<IArtifactCoordinator>(_key.Value).ShouldBeOfType<ArtifactCoordinator>();
        _ = provider.GetRequiredService<IArtifactIntegrityValidator>().ShouldBeOfType<DefaultArtifactIntegrityValidator>();
        _ = provider.GetRequiredService<IArtifactRetentionPolicy>().ShouldBeOfType<DefaultArtifactRetentionPolicy>();
        _ = provider.GetRequiredService<IArtifactEventDispatcher>().ShouldBeOfType<ArtifactEventDispatcher>();
        provider.GetRequiredService<IIdentifierGenerator<ArtifactId>>().Create().Value.ShouldNotBe(Guid.Empty);
        provider.GetRequiredService<IIdentifierGenerator<ArtifactPreparationId>>().Create().Value.ShouldNotBe(Guid.Empty);
        provider.GetKeyedService<IArtifactStore>(ArtifactTestData.BackendKey.Value).ShouldBeNull();
        provider.GetService<IArtifactStore>().ShouldBeNull();
        provider.GetRequiredService<IArtifactCoordinatorCatalog>().ShouldBeOfType<DefaultArtifactCoordinatorCatalog>().TryGet(_key, out var snapshot).ShouldBeTrue();
        snapshot!.Backends.ShouldBe([ArtifactTestData.BackendKey]);
    }

    [Fact]
    public void AddAgentArtifacts_WhenTheProfileIsMissing_FailsWhenTheCoordinatorIsResolved()
    {
        var services = Composition(withProfile: false);
        _ = services.AddAgentArtifacts(_key, _profile);
        using var provider = services.BuildServiceProvider();

        Should.Throw<InvalidOperationException>(() => provider.GetRequiredKeyedService<IArtifactCoordinator>(_key.Value)).Message.ShouldContain("not registered");
    }

    [Fact]
    public void AddAgentArtifacts_WhenRepeatedIdentically_IsIdempotentAndConflictingRegistrationIsRejected()
    {
        var services = Composition();
        _ = services.AddAgentArtifacts(_key, _profile, static options => options.MaximumArtifactBytes = 10);
        var count = services.Count;

        _ = services.AddAgentArtifacts(_key, _profile, static options => options.MaximumArtifactBytes = 10);

        services.Count.ShouldBe(count);
        _ = Should.Throw<InvalidOperationException>(() => services.AddAgentArtifacts(_key, _profile, static options => options.MaximumArtifactBytes = 11));
        _ = Should.Throw<InvalidOperationException>(() => services.AddAgentArtifacts(_key, new ArtifactProfileKey("other"), static options => options.MaximumArtifactBytes = 10));
    }

    [Fact]
    public void AddAgentArtifacts_WhenTwoKeysShareAProfile_ResolvesIndependentCoordinators()
    {
        var services = Composition();
        _ = services.AddAgentArtifacts(_key, _profile);
        _ = services.AddAgentArtifacts(new ComponentKey<IArtifactCoordinator>("two"), _profile);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredKeyedService<IArtifactCoordinator>("one").ShouldNotBeSameAs(provider.GetRequiredKeyedService<IArtifactCoordinator>("two"));
        _ = provider.GetRequiredService<IProcessOutputArtifactSink>().ShouldBeOfType<ArtifactProcessOutputSink>();
        _ = provider.GetRequiredKeyedService<IProcessOutputArtifactSink>("two").ShouldBeOfType<ArtifactProcessOutputSink>();
    }

    [Fact]
    public void AddArtifactProfile_WhenRepeatedIdenticallyOrWithANewVersion_IsIdempotentOrRetainsBoth()
    {
        var services = new ServiceCollection();
        _ = services.AddArtifactProfile(_profile, Profile);
        var count = services.Count;

        _ = services.AddArtifactProfile(_profile, Profile);
        _ = services.AddArtifactProfile(_profile, options =>
        {
            Profile(options);
            options.Version = new ArtifactProfileVersion(2);
        });
        using var provider = services.BuildServiceProvider();

        count.ShouldBe(1);
        provider.GetKeyedServices<ArtifactProfileSnapshot>(_profile.Value).Select(static snapshot => snapshot.Version.Value).Order().ShouldBe([1L, 2L]);
    }

    [Fact]
    public void AddArtifactProfile_WhenTheSameVersionConflicts_ThrowsUnlessReplaced()
    {
        var services = new ServiceCollection();
        _ = services.AddArtifactProfile(_profile, Profile);

        _ = Should.Throw<InvalidOperationException>(() => services.AddArtifactProfile(_profile, options =>
        {
            Profile(options);
            options.Routes[new ArtifactDirectoryId("out")] = new ArtifactBackendKey("other");
        }));
        _ = services.ReplaceArtifactProfile(_profile, options =>
        {
            Profile(options);
            options.Routes[new ArtifactDirectoryId("out")] = new ArtifactBackendKey("other");
        });
        using var provider = services.BuildServiceProvider();

        provider.GetKeyedServices<ArtifactProfileSnapshot>(_profile.Value).ShouldHaveSingleItem().Routes[new ArtifactDirectoryId("out")].ShouldBe(new ArtifactBackendKey("other"));
    }

    [Fact]
    public void AddArtifactProfile_WhenTheProfileIsInvalid_ThrowsAtRegistration()
    {
        var services = new ServiceCollection();

        _ = Should.Throw<InvalidOperationException>(() => services.AddArtifactProfile(_profile, NoProfile));
    }

    [Fact]
    public void AddArtifactStore_WhenRegisteredTwiceOrConflicting_IsIdempotentOrRejectedUnlessReplaced()
    {
        var services = new ServiceCollection();
        var key = new ArtifactBackendKey("store");
        _ = services.AddArtifactStore<FirstStore>(key);
        var count = services.Count;

        _ = services.AddArtifactStore<FirstStore>(key);
        _ = Should.Throw<InvalidOperationException>(() => services.AddArtifactStore<SecondStore>(key));
        _ = services.ReplaceArtifactStore<SecondStore>(key);

        count.ShouldBe(1);
        services.Count.ShouldBe(1);
        services.Single().KeyedImplementationType.ShouldBe(typeof(SecondStore));
    }

    [Fact]
    public void AddArtifactEventSink_WhenRepeatedOrConflicting_IsIdempotentOrRejected()
    {
        var services = new ServiceCollection();
        var registration = Sink("audit");
        _ = services.AddArtifactEventSink<RecordingArtifactEventSink>(_key, registration);
        var count = services.Count;

        _ = services.AddArtifactEventSink<RecordingArtifactEventSink>(_key, Sink("audit"));
        _ = Should.Throw<InvalidOperationException>(() => services.AddArtifactEventSink<OtherSink>(_key, registration));
        _ = services.AddArtifactEventSink<OtherSink>(_key, Sink("other"));

        services.Count.ShouldBe(count + 2);
    }

    [Fact]
    public void ReplaceArtifactValidatorAndPolicy_WhenCalled_ReplaceTheEngineWideSingletons()
    {
        var services = Composition();
        _ = services.AddAgentArtifacts(_key, _profile);
        _ = services.ReplaceArtifactIntegrityValidator<AlternateValidator>();
        _ = services.ReplaceArtifactRetentionPolicy<AlternatePolicy>();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IArtifactIntegrityValidator>().ShouldBeOfType<AlternateValidator>();
        _ = provider.GetRequiredService<IArtifactRetentionPolicy>().ShouldBeOfType<AlternatePolicy>();
    }

    [Fact]
    public void AddInMemoryArtifactStore_WhenKeyedAndRepeated_RegistersOneStorePerKeyAndNothingUnkeyed()
    {
        var services = Composition();
        var key = new ArtifactBackendKey("memory");
        _ = services.AddInMemoryArtifactStore(key);
        _ = services.AddInMemoryArtifactStore(key);
        _ = services.AddInMemoryArtifactStore(new ArtifactBackendKey("other"));
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredKeyedService<IArtifactStore>("memory").ShouldNotBeSameAs(provider.GetRequiredKeyedService<IArtifactStore>("other"));
        provider.GetRequiredKeyedService<IArtifactStore>("memory").ShouldBeSameAs(provider.GetRequiredKeyedService<IArtifactStore>("memory"));
        provider.GetService<IArtifactStore>().ShouldBeNull();
    }

    [Fact]
    public void AddInMemoryArtifactStore_WhenArgumentsAreInvalid_ThrowsNamingThem()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddInMemoryArtifactStore(new ArtifactBackendKey("k"))).ParamName.ShouldBe("services");
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddInMemoryArtifactStore(default)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => _ = services.AddInMemoryArtifactReferenceCommitIntentStore()).ParamName.ShouldBe("services");
    }

    private static ServiceCollection Composition(bool withProfile = true)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TimeProvider.System);
        _ = services.AddLogging();
        _ = services.AddSingleton<ISecurityGrantStore>(new Permissions.InMemory.InMemorySecurityGrantStore(TimeProvider.System));
        _ = services.AddSingleton<ISecurityAuthoritySelector>(new FixedSecurityAuthoritySelector(new ScriptedSecurityAuthority()));
        _ = services.AddSingleton<IIdentifierGenerator<SecurityRequestId>>(new SequentialIdentifierGenerator<SecurityRequestId>(static value => new SecurityRequestId(value), 1));
        if (withProfile)
        {
            _ = services.AddArtifactProfile(_profile, Profile);
        }

        return services;
    }

    private static void NoProfile(ArtifactProfileOptions options) => _ = options;

    private static void Profile(ArtifactProfileOptions options)
    {
        options.DefaultDirectory = new ArtifactDirectoryId("out");
        options.Routes[new ArtifactDirectoryId("out")] = ArtifactTestData.BackendKey;
    }

    private static ArtifactEventSinkRegistration Sink(string id) => new(new ArtifactEventSinkId(id), 0, ServiceLifetime.Singleton);

    private sealed class OtherSink: IArtifactEventSink
    {
        public ValueTask PublishAsync(ArtifactEvent artifactEvent, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class AlternateValidator: IArtifactIntegrityValidator
    {
        public string Algorithm => "alt";

        public ValueTask<ArtifactIntegrityResult> ValidateAsync(ReadOnlyMemory<byte> content, long declaredLength, ContentHash? declaredContentHash, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ArtifactIntegrityResult>(new ArtifactIntegrityVerified(new ContentHash("alt:x")));
    }

    private sealed class AlternatePolicy: IArtifactRetentionPolicy
    {
        public ValueTask<ArtifactRetentionDecision> ResolveAsync(ArtifactRetentionRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ArtifactRetentionDecision>(new ArtifactRetentionAllowed(request.ProfileDefault));

        public ValueTask<ArtifactRetentionDecision> EvaluateDeletionAsync(ArtifactReference reference, DateTimeOffset now, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ArtifactRetentionDecision>(new ArtifactRetentionAllowed(reference.Retention));
    }

    private abstract class StoreBase: IArtifactStore
    {
        public ComponentId SecurityAudience { get; } = new("test");

        public Task<ArtifactStorePrepareResult> PrepareAsync(ArtifactStorePrepareRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<ArtifactStoreFinalizeResult> FinalizeAsync(ArtifactStoreFinalizeRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<ArtifactStoreAbortResult> AbortAsync(ArtifactStoreAbortRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ArtifactStoreReadResult> ReadAsync(ArtifactStoreReadRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<ArtifactStoreDeleteResult> DeleteAsync(ArtifactStoreDeleteRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FirstStore: StoreBase;

    private sealed class SecondStore: StoreBase;

    private sealed class InMemoryCoordinator: IArtifactCoordinator
    {
        public Task<ArtifactPrepareResult> PrepareAsync(ArtifactPrepareRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<ArtifactFinalizeResult> FinalizeAsync(ArtifactFinalizeRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<ArtifactAbortResult> AbortAsync(ArtifactAbortRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ArtifactReadResult> ReadAsync(ArtifactReadRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<ArtifactDeleteResult> DeleteAsync(ArtifactDeleteRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<ArtifactReconciliationResult> ReconcileAsync(ArtifactReconciliationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
