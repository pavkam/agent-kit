// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;

/// <summary>
/// Exercises catalog composition, alias uniqueness across sources, snapshot
/// stability, and source-identity validation.
/// </summary>
public sealed class DefaultModelCatalogTests
{
    [Fact]
    public async Task GetSnapshotAsync_WhenNoSourcesRegistered_ReturnsEmptyCatalog()
    {
        var catalog = CreateCatalog();

        var snapshot = await catalog.GetSnapshotAsync(TestContext.Current.CancellationToken);

        snapshot.ConversationModels.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetSnapshotAsync_ComposesEveryRegisteredSourceInOrder()
    {
        var catalog = CreateCatalog(
            Source("first", ProviderTestData.Model("a")),
            Source("second", ProviderTestData.Model("b")));

        var snapshot = await catalog.GetSnapshotAsync(TestContext.Current.CancellationToken);

        snapshot.ConversationModels.Select(model => model.Alias.Value).ShouldBe(["a", "b"]);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenCalledTwice_ReturnsSameImmutableSnapshot()
    {
        var catalog = CreateCatalog(Source("first", ProviderTestData.Model("a")));

        var first = await catalog.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var second = await catalog.GetSnapshotAsync(TestContext.Current.CancellationToken);

        second.ShouldBeSameAs(first);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenTwoSourcesPublishSameAlias_ThrowsInvalidOperationException()
    {
        var catalog = CreateCatalog(
            Source("first", ProviderTestData.Model("shared")),
            Source("second", ProviderTestData.Model("shared")));

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await catalog.GetSnapshotAsync(TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("shared");
        exception.Message.ShouldContain("first");
        exception.Message.ShouldContain("second");
    }

    [Fact]
    public async Task RefreshAsync_PublishesAMonotonicallyIncreasingVersion()
    {
        var catalog = CreateCatalog(Source("first", ProviderTestData.Model("a")));

        var first = await catalog.RefreshAsync(TestContext.Current.CancellationToken);
        var second = await catalog.RefreshAsync(TestContext.Current.CancellationToken);

        second.Version.Value.ShouldBeGreaterThan(first.Version.Value);
    }

    [Fact]
    public async Task RefreshAsync_WhenSourceFails_LeavesLastGoodSnapshotActive()
    {
        var failing = new TogglingSource(new ModelDescriptorSourceId("toggling"));
        var catalog = CreateCatalog(failing);

        var good = await catalog.GetSnapshotAsync(TestContext.Current.CancellationToken);
        failing.ShouldFail = true;

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await catalog.RefreshAsync(TestContext.Current.CancellationToken));

        var current = await catalog.GetSnapshotAsync(TestContext.Current.CancellationToken);
        current.ShouldBeSameAs(good);
    }

    [Fact]
    public void Constructor_WhenTwoSourcesShareAnId_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => CreateCatalog(
            Source("duplicate", ProviderTestData.Model("a")),
            Source("duplicate", ProviderTestData.Model("b"))));

        exception.ParamName.ShouldBe("sources");
    }

    [Fact]
    public void Constructor_WhenSourcesIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new DefaultModelCatalog(null!, NullLogger<DefaultModelCatalog>.Instance));

        exception.ParamName.ShouldBe("sources");
    }

    [Fact]
    public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new DefaultModelCatalog([], null!));

        exception.ParamName.ShouldBe("logger");
    }

    private static DefaultModelCatalog CreateCatalog(params IModelDescriptorSource[] sources) =>
        new(sources, NullLogger<DefaultModelCatalog>.Instance);

    private static StaticModelDescriptorSource Source(string id, params ModelDescriptor[] models) =>
        new(new ModelDescriptorSourceId(id), [.. models]);

    /// <summary>
    /// A source that can be switched to fail, used to prove that a broken
    /// refresh does not replace the last good snapshot.
    /// </summary>
    private sealed class TogglingSource(ModelDescriptorSourceId sourceId): IModelDescriptorSource
    {
        public bool ShouldFail { get; set; }

        public ModelDescriptorSourceId SourceId { get; } = sourceId;

        public ValueTask<ModelDescriptorSourceSnapshot> ReadAsync(
            CancellationToken cancellationToken = default) =>
            ShouldFail
                ? throw new InvalidOperationException("Discovery failed.")
                : ValueTask.FromResult(new ModelDescriptorSourceSnapshot(
                    SourceId,
                    new ModelDescriptorSourceVersion(1),
                    [ProviderTestData.Model("a")]));
    }
}
