// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

using AgentKit.Permissions.InMemory;

/// <summary>Composes one real coordinator over in-memory stores, a scripted authority, and a fake clock through the public registration surface.</summary>
internal sealed class CoordinatorHarness: IDisposable
{
    private readonly ServiceProvider _provider;

    private CoordinatorHarness(ServiceProvider provider, FakeTimeProvider clock, InMemorySecurityGrantStore grants, ScriptedSecurityAuthority authority, RecordingArtifactEventSink sink)
    {
        _provider = provider;
        Clock = clock;
        Grants = grants;
        Authority = authority;
        Sink = sink;
    }

    /// <summary>Gets the fake clock every component observes.</summary>
    internal FakeTimeProvider Clock { get; }

    /// <summary>Gets the authoritative grant store stores consume from.</summary>
    internal InMemorySecurityGrantStore Grants { get; }

    /// <summary>Gets the scripted authority.</summary>
    internal ScriptedSecurityAuthority Authority { get; }

    /// <summary>Gets the event sink registered for the coordinator.</summary>
    internal RecordingArtifactEventSink Sink { get; }

    /// <summary>Gets the composed provider.</summary>
    internal IServiceProvider Services => _provider;

    /// <summary>Gets the keyed coordinator under test.</summary>
    internal IArtifactCoordinator Coordinator => _provider.GetRequiredKeyedService<IArtifactCoordinator>(ArtifactTestData.CoordinatorKey.Value);

    /// <summary>Gets the keyed in-memory store the profile routes to.</summary>
    internal IArtifactStore Store => _provider.GetRequiredKeyedService<IArtifactStore>(ArtifactTestData.BackendKey.Value);

    /// <summary>Gets the reference-commit intent store, when registered.</summary>
    internal IArtifactReferenceCommitIntentStore? Intents => _provider.GetService<IArtifactReferenceCommitIntentStore>();

    /// <summary>Creates a harness with one profile, one in-memory backend, and a recording sink.</summary>
    /// <param name="configureOptions">Adjusts the coordinator mechanics.</param>
    /// <param name="configureProfile">Adjusts the profile.</param>
    /// <param name="withIntents">Whether the in-memory intent store is registered.</param>
    /// <param name="extra">Adds further registrations before the provider is built; it runs before the default store registration, so a pre-registered keyed store wins.</param>
    /// <param name="clock">A shared clock, or a new one.</param>
    /// <param name="grants">A shared grant store, or a new one.</param>
    /// <returns>A harness owning its provider.</returns>
    internal static CoordinatorHarness Create(
        Action<AgentArtifactOptions>? configureOptions = null,
        Action<ArtifactProfileOptions>? configureProfile = null,
        bool withIntents = false,
        Action<IServiceCollection>? extra = null,
        FakeTimeProvider? clock = null,
        InMemorySecurityGrantStore? grants = null)
    {
        clock ??= new FakeTimeProvider(ArtifactTestData.Now);
        grants ??= new InMemorySecurityGrantStore(clock);
        var authority = new ScriptedSecurityAuthority(grants);
        var sink = new RecordingArtifactEventSink();
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(clock);
        _ = services.AddSingleton<ISecurityGrantStore>(grants);
        _ = services.AddSingleton<ISecurityAuthoritySelector>(new FixedSecurityAuthoritySelector(authority));
        _ = services.AddSingleton<IIdentifierGenerator<SecurityRequestId>>(new SequentialIdentifierGenerator<SecurityRequestId>(static value => new SecurityRequestId(value), 0x51));
        _ = services.AddSingleton<IIdentifierGenerator<ArtifactId>>(new SequentialIdentifierGenerator<ArtifactId>(static value => new ArtifactId(value), 0x52));
        _ = services.AddSingleton<IIdentifierGenerator<ArtifactPreparationId>>(new SequentialIdentifierGenerator<ArtifactPreparationId>(static value => new ArtifactPreparationId(value), 0x53));
        _ = services.AddArtifactProfile(ArtifactTestData.ProfileKey, profile =>
        {
            profile.DefaultDirectory = ArtifactTestData.Directory;
            profile.Routes[ArtifactTestData.Directory] = ArtifactTestData.BackendKey;
            configureProfile?.Invoke(profile);
        });
        extra?.Invoke(services);
        _ = services.AddInMemoryArtifactStore(ArtifactTestData.BackendKey);
        _ = services.AddAgentArtifacts(ArtifactTestData.CoordinatorKey, ArtifactTestData.ProfileKey, configureOptions);
        _ = services.AddSingleton(sink);
        _ = services.AddArtifactEventSink<RecordingArtifactEventSink>(
            ArtifactTestData.CoordinatorKey,
            new ArtifactEventSinkRegistration(new ArtifactEventSinkId("recording"), 0, ServiceLifetime.Singleton));
        if (withIntents)
        {
            _ = services.AddInMemoryArtifactReferenceCommitIntentStore();
        }

        return new CoordinatorHarness(services.BuildServiceProvider(), clock, grants, authority, sink);
    }

    /// <summary>Prepares and finalizes content, returning the committed reference.</summary>
    /// <param name="content">The complete content.</param>
    /// <param name="metadata">Optional metadata overriding the defaults.</param>
    /// <param name="key">The replay key.</param>
    /// <returns>The committed reference.</returns>
    internal async Task<ArtifactReference> CommitAsync(byte[] content, ArtifactMetadata? metadata = null, string key = "prepare-1")
    {
        var prepared = (await Coordinator.PrepareAsync(ArtifactTestData.Prepare(content, metadata, key: key), TestContext.Current.CancellationToken))
            .ShouldBeOfType<ArtifactPrepared>();
        return (await Coordinator.FinalizeAsync(ArtifactTestData.Finalize(prepared.PreparationId, key: $"{key}:finalize"), TestContext.Current.CancellationToken))
            .ShouldBeOfType<ArtifactFinalized>().Reference;
    }

    /// <summary>Reads committed content as text and disposes the owned stream.</summary>
    /// <param name="reference">The committed reference.</param>
    /// <returns>The content.</returns>
    internal async Task<string> ReadTextAsync(ArtifactReference reference)
    {
        await using var opened = (await Coordinator.ReadAsync(ArtifactTestData.Read(reference), TestContext.Current.CancellationToken))
            .ShouldBeOfType<ArtifactReadOpened>();
        using var reader = new StreamReader(opened.Content);
        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose() => _provider.Dispose();
}
