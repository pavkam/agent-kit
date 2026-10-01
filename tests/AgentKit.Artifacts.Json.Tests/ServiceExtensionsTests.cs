// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Json.Tests;

/// <summary>Verifies the JSON artifact-store registration is explicit, keyed, idempotent, and never chooses a persistence target.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddJsonArtifactStore_WhenArgumentsAreInvalid_ThrowsNamingThem()
    {
        using var root = new JsonArtifactTestRoot();
        IServiceCollection services = null!;
        var key = new ArtifactBackendKey("json");

        Should.Throw<ArgumentNullException>(() => services.AddJsonArtifactStore(key, root.Target())).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddJsonArtifactStore(key, null!)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddJsonArtifactStore(default, root.Target())).ParamName.ShouldBe("key");
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new ServiceCollection().AddJsonArtifactStore(key, root.Target(), static options => options.MaximumPayloadBytes = 0));
    }

    [Fact]
    public async Task AddJsonArtifactStore_WhenRegistered_ResolvesOneKeyedStoreThatOpensLazily()
    {
        using var root = new JsonArtifactTestRoot();
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(new Permissions.InMemory.InMemorySecurityGrantStore(TimeProvider.System));
        _ = services.AddJsonArtifactStore(new ArtifactBackendKey("json"), root.Target());
        _ = services.AddJsonArtifactStore(new ArtifactBackendKey("json"), root.Target("ignored"));
        await using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredKeyedService<IArtifactStore>("json");

        store.ShouldBeOfType<JsonArtifactStore>().ShouldBeSameAs(provider.GetRequiredKeyedService<IArtifactStore>("json"));
        File.Exists(root.ManifestPath()).ShouldBeFalse();
        provider.GetService<IArtifactStore>().ShouldBeNull();
        await ((JsonArtifactStore) store).InitializeAsync(TestContext.Current.CancellationToken);
        File.Exists(root.ManifestPath()).ShouldBeTrue();
        File.Exists(root.ManifestPath("ignored")).ShouldBeFalse();
    }
}
