// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

/// <summary>Verifies <see cref="DefaultArtifactCoordinatorCatalog"/> publishes exact composition evidence from registered profiles.</summary>
public sealed class DefaultArtifactCoordinatorCatalogTests
{
    private static readonly ComponentKey<IArtifactCoordinator> _key = new("one");
    private static readonly ArtifactProfileKey _profile = new("profile");

    [Fact]
    public void Constructor_WhenArgumentsAreNull_ThrowsNamingThem()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        Should.Throw<ArgumentNullException>(() => new DefaultArtifactCoordinatorCatalog(null!, provider)).ParamName.ShouldBe("registrations");
        Should.Throw<ArgumentNullException>(() => new DefaultArtifactCoordinatorCatalog([], null!)).ParamName.ShouldBe("services");
    }

    [Fact]
    public void TryGet_WhenKeyIsBlank_ThrowsArgumentException()
    {
        var catalog = Catalog(out var provider);
        using var owner = provider;

        Should.Throw<ArgumentException>(() => catalog.TryGet(default, out _)).ParamName.ShouldBe("key");
    }

    [Fact]
    public void TryGet_WhenTheKeyIsRegistered_ReturnsTheRoutedBackends()
    {
        var catalog = Catalog(out var provider);
        using var owner = provider;

        catalog.TryGet(_key, out var snapshot).ShouldBeTrue();

        snapshot!.Key.ShouldBe(_key);
        snapshot.ProfileKey.ShouldBe(_profile);
        snapshot.ProfileVersion.ShouldBe(new ArtifactProfileVersion(1));
        snapshot.Backends.ShouldBe([ArtifactTestData.BackendKey]);
    }

    [Fact]
    public void TryGet_WhenTheKeyIsUnknown_ReturnsFalse()
    {
        var catalog = Catalog(out var provider);
        using var owner = provider;

        catalog.TryGet(new ComponentKey<IArtifactCoordinator>("other"), out var snapshot).ShouldBeFalse();

        snapshot.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenTheProfileIsNotRegistered_ThrowsInvalidOperationException()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        Should.Throw<InvalidOperationException>(() => new DefaultArtifactCoordinatorCatalog([Registration()], provider)).Message.ShouldContain("not registered");
    }

    private static DefaultArtifactCoordinatorCatalog Catalog(out ServiceProvider provider)
    {
        var services = new ServiceCollection();
        _ = services.AddArtifactProfile(_profile, static options =>
        {
            options.DefaultDirectory = new ArtifactDirectoryId("out");
            options.Routes[new ArtifactDirectoryId("out")] = ArtifactTestData.BackendKey;
        });
        provider = services.BuildServiceProvider();
        return new DefaultArtifactCoordinatorCatalog([Registration()], provider);
    }

    private static ArtifactCoordinatorRegistration Registration() => new(_key, _profile, AgentArtifactOptionsSnapshot.Create(new AgentArtifactOptions()));
}
