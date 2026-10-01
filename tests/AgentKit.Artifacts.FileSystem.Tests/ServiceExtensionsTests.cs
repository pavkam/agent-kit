// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.FileSystem.Tests;

/// <summary>Verifies the file-system artifact-store registration is explicit, keyed, idempotent, and performs no effect until used.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public async Task AddFileSystemArtifactStore_WhenArgumentsAreInvalid_ThrowsNamingThem()
    {
        await using var fixture = new FileSystemArtifactStoreConformanceFixture();
        IServiceCollection services = null!;
        var key = new ArtifactBackendKey("fs");

        Should.Throw<ArgumentNullException>(() => services.AddFileSystemArtifactStore(key, fixture.Target)).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddFileSystemArtifactStore(key, null!)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddFileSystemArtifactStore(default, fixture.Target)).ParamName.ShouldBe("key");
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new ServiceCollection().AddFileSystemArtifactStore(key, fixture.Target, static options => options.MaximumPayloadBytes = 0));
    }

    [Fact]
    public async Task AddFileSystemArtifactStore_WhenRegistered_ResolvesOneKeyedStoreAndPerformsNoEffect()
    {
        await using var fixture = new FileSystemArtifactStoreConformanceFixture();
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(fixture.GrantStore);
        _ = services.AddSingleton<IFileSystemSelector>(fixture.Selector);
        _ = services.AddSingleton<ISecurityAuthoritySelector>(new FixedSecurityAuthoritySelector(fixture.Authority));
        _ = services.AddFileSystemArtifactStore(new ArtifactBackendKey("fs"), fixture.Target);
        _ = services.AddFileSystemArtifactStore(new ArtifactBackendKey("fs"), fixture.Target);
        await using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredKeyedService<IArtifactStore>("fs");

        store.ShouldBeOfType<FileSystemArtifactStore>().ShouldBeSameAs(provider.GetRequiredKeyedService<IArtifactStore>("fs"));
        provider.GetService<IArtifactStore>().ShouldBeNull();
        fixture.Authority.Requests.ShouldBeEmpty();
    }
}
