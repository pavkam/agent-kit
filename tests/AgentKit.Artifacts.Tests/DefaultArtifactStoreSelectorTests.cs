// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

/// <summary>Verifies <see cref="DefaultArtifactStoreSelector"/> routes only through explicit keyed registrations.</summary>
public sealed class DefaultArtifactStoreSelectorTests
{
    [Fact]
    public async Task SelectAsync_WhenTheDirectoryIsRoutedAndItsStoreRegistered_ReturnsThatStore()
    {
        var store = new InMemoryArtifactStore(new Permissions.InMemory.InMemorySecurityGrantStore(TimeProvider.System), TimeProvider.System);
        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton<IArtifactStore>("backend", store);
        using var provider = services.BuildServiceProvider();
        var selector = new DefaultArtifactStoreSelector(provider);

        var selected = (await selector.SelectAsync(Profile(), ArtifactTestData.Directory, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStoreSelected>();

        selected.Backend.ShouldBe(ArtifactTestData.BackendKey);
        selected.Store.ShouldBeSameAs(store);
    }

    [Fact]
    public async Task SelectAsync_WhenTheDirectoryIsNotRouted_ReportsUnavailable()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        var result = await new DefaultArtifactStoreSelector(provider).SelectAsync(Profile(), new ArtifactDirectoryId("elsewhere"), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactStoreUnavailable>().Failure.Kind.ShouldBe(ArtifactFailureKind.Unavailable);
    }

    [Fact]
    public async Task SelectBackendAsync_WhenNoStoreIsRegistered_ReportsUnavailableAndNeverFabricatesOne()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        var result = await new DefaultArtifactStoreSelector(provider).SelectBackendAsync(ArtifactTestData.BackendKey, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactStoreUnavailable>().Failure.Kind.ShouldBe(ArtifactFailureKind.Unavailable);
        provider.GetService<IArtifactStore>().ShouldBeNull();
    }

    [Fact]
    public async Task Selection_WhenArgumentsAreInvalid_ThrowsNamingThem()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var selector = new DefaultArtifactStoreSelector(provider);

        Should.Throw<ArgumentNullException>(() => new DefaultArtifactStoreSelector(null!)).ParamName.ShouldBe("services");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await selector.SelectAsync(null!, ArtifactTestData.Directory, TestContext.Current.CancellationToken))).ParamName.ShouldBe("profile");
        (await Should.ThrowAsync<ArgumentException>(async () => await selector.SelectBackendAsync(default, TestContext.Current.CancellationToken))).ParamName.ShouldBe("backend");
    }

    [Fact]
    public async Task Selection_WhenCancelled_Throws()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var selector = new DefaultArtifactStoreSelector(provider);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await selector.SelectAsync(Profile(), ArtifactTestData.Directory, cancelled.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await selector.SelectBackendAsync(ArtifactTestData.BackendKey, cancelled.Token));
    }

    private static ArtifactProfileSnapshot Profile() => ArtifactProfileSnapshot.Create(
        ArtifactTestData.ProfileKey,
        new ArtifactProfileOptions { DefaultDirectory = ArtifactTestData.Directory, Routes = { [ArtifactTestData.Directory] = ArtifactTestData.BackendKey } });
}
