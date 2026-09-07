// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using System.Collections.Immutable;

/// <summary>
/// Exercises catalog composition, source precedence, snapshot stability, and
/// resolution outcomes.
/// </summary>
public sealed class DefaultAgentDefinitionCatalogTests
{
    [Fact]
    public void CatalogArgumentGuards_WhenCollectionsAreEmpty_DoNotThrow()
    {
        var sources = ImmutableArray<IAgentDefinitionSource>.Empty;
        var snapshots = ImmutableArray<AgentDefinitionSourceSnapshot>.Empty;

        Should.NotThrow(() => ArgumentException.ThrowIfDuplicateAgentDefinitionSourceIds(sources));
        Should.NotThrow(() => ArgumentException.ThrowIfDuplicateAgentDefinitionSnapshotSourceIds(snapshots));
        Should.NotThrow(() => ArgumentException.ThrowIfUnknownAgentDefinitionSource(snapshots, sources));
    }

    [Fact]
    public void ThrowIfDuplicateAgentDefinitionSourceIds_WhenArrayIsDefault_ThrowsWithExactParameterName()
    {
        ImmutableArray<IAgentDefinitionSource> sources = default;

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfDuplicateAgentDefinitionSourceIds(sources));

        exception.ParamName.ShouldBe("sources");
    }

    [Fact]
    public void ThrowIfDuplicateAgentDefinitionSourceIds_WhenItemIsNull_ThrowsWithExactParameterName()
    {
        ImmutableArray<IAgentDefinitionSource> sources = [null!];

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfDuplicateAgentDefinitionSourceIds(sources));

        exception.ParamName.ShouldBe("sources");
    }

    [Fact]
    public void ThrowIfDuplicateAgentDefinitionSourceIds_WhenSourceIdIsDefault_ThrowsWithExactParameterName()
    {
        var sources = ImmutableArray.Create<IAgentDefinitionSource>(new DefaultIdAgentDefinitionSource());

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfDuplicateAgentDefinitionSourceIds(sources));

        exception.ParamName.ShouldBe("sources");
    }

    [Fact]
    public void ThrowIfDuplicateAgentDefinitionSnapshotSourceIds_WhenArrayIsDefault_ThrowsWithExactParameterName()
    {
        ImmutableArray<AgentDefinitionSourceSnapshot> snapshots = default;

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfDuplicateAgentDefinitionSnapshotSourceIds(snapshots));

        exception.ParamName.ShouldBe("snapshots");
    }

    [Fact]
    public void ThrowIfDuplicateAgentDefinitionSnapshotSourceIds_WhenItemIsNull_ThrowsWithExactParameterName()
    {
        ImmutableArray<AgentDefinitionSourceSnapshot> snapshots = [null!];

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfDuplicateAgentDefinitionSnapshotSourceIds(snapshots));

        exception.ParamName.ShouldBe("snapshots");
    }

    [Fact]
    public void ThrowIfUnknownAgentDefinitionSource_WhenSourcesContainNull_UsesSourcesParameterName()
    {
        var snapshots = ImmutableArray<AgentDefinitionSourceSnapshot>.Empty;
        ImmutableArray<IAgentDefinitionSource> sources = [null!];

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfUnknownAgentDefinitionSource(snapshots, sources));

        exception.ParamName.ShouldBe("sources");
    }

    [Fact]
    public void ThrowIfUnknownAgentDefinitionSource_WhenSnapshotsAreDefault_UsesSnapshotsParameterName()
    {
        ImmutableArray<AgentDefinitionSourceSnapshot> snapshots = default;
        var sources = ImmutableArray<IAgentDefinitionSource>.Empty;

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfUnknownAgentDefinitionSource(snapshots, sources));

        exception.ParamName.ShouldBe("snapshots");
    }

    [Fact]
    public void ThrowIfUnknownAgentDefinitionSource_WhenSourcesAreDefault_UsesSourcesParameterName()
    {
        var snapshots = ImmutableArray<AgentDefinitionSourceSnapshot>.Empty;
        ImmutableArray<IAgentDefinitionSource> sources = default;

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfUnknownAgentDefinitionSource(snapshots, sources));

        exception.ParamName.ShouldBe("sources");
    }

    [Fact]
    public void ThrowIfUnknownAgentDefinitionSource_WhenSnapshotIsNull_UsesSnapshotsParameterName()
    {
        ImmutableArray<AgentDefinitionSourceSnapshot> snapshots = [null!];
        var sources = ImmutableArray<IAgentDefinitionSource>.Empty;

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfUnknownAgentDefinitionSource(snapshots, sources));

        exception.ParamName.ShouldBe("snapshots");
    }

    [Fact]
    public void ThrowIfDuplicateAgentDefinitionSourceIds_WhenDuplicateExists_UsesInferredParameterName()
    {
        var sources = ImmutableArray.Create<IAgentDefinitionSource>(
            new FakeAgentDefinitionSource("duplicate", 0),
            new FakeAgentDefinitionSource("duplicate", 1));

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfDuplicateAgentDefinitionSourceIds(sources));

        exception.ParamName.ShouldBe("sources");
    }

    [Fact]
    public void ThrowIfDuplicateAgentDefinitionSnapshotSourceIds_WhenUnique_DoesNotThrow()
    {
        var snapshots = ImmutableArray.Create(
            new AgentDefinitionSourceSnapshot(new AgentDefinitionSourceId("a"), new AgentDefinitionSourceVersion(1), 0, []),
            new AgentDefinitionSourceSnapshot(new AgentDefinitionSourceId("b"), new AgentDefinitionSourceVersion(1), 0, []));

        Should.NotThrow(() => ArgumentException.ThrowIfDuplicateAgentDefinitionSnapshotSourceIds(snapshots));
    }

    [Fact]
    public void ThrowIfUnknownAgentDefinitionSource_WhenSnapshotIsUnknown_UsesInferredParameterName()
    {
        var sources = ImmutableArray.Create<IAgentDefinitionSource>(new FakeAgentDefinitionSource("known", 0));
        var snapshots = ImmutableArray.Create(
            new AgentDefinitionSourceSnapshot(new AgentDefinitionSourceId("unknown"), new AgentDefinitionSourceVersion(1), 0, []));

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfUnknownAgentDefinitionSource(snapshots, sources));

        exception.ParamName.ShouldBe("snapshots");
    }

    [Fact]
    public void Constructor_WhenBootstrapSourcesAreDuplicatedDespitePartialCoverage_ThrowsArgumentException()
    {
        var source = new PublicationMutableSource(CompositionTestData.Definition());
        var snapshot = new AgentDefinitionSourceSnapshot(
            source.SourceId,
            new AgentDefinitionSourceVersion(1),
            0,
            [source.Definition]);

        var exception = Should.Throw<ArgumentException>(
            () => new DefaultAgentDefinitionCatalog([source, new FakeAgentDefinitionSource("other", 0)], [snapshot, snapshot]));

        exception.ParamName.ShouldBe("bootstrapSnapshots");
    }

    [Fact]
    public void Constructor_WhenBootstrapNamesUnknownSource_ThrowsArgumentException()
    {
        var source = new PublicationMutableSource(CompositionTestData.Definition());
        var snapshot = new AgentDefinitionSourceSnapshot(
            new AgentDefinitionSourceId("unknown"),
            new AgentDefinitionSourceVersion(1),
            0,
            [source.Definition]);

        var exception = Should.Throw<ArgumentException>(
            () => new DefaultAgentDefinitionCatalog([source], [snapshot]));

        exception.ParamName.ShouldBe("bootstrapSnapshots");
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenCachedAndTokenIsCanceled_HonorsCancellation()
    {
        using var catalog = new DefaultAgentDefinitionCatalog([
            new FakeAgentDefinitionSource("a", 0, CompositionTestData.Definition()),
        ]);
        _ = await catalog.GetSnapshotAsync(TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await catalog.GetSnapshotAsync(cancellation.Token));
    }

    [Fact]
    public async Task RefreshAsync_WhenSourceIdentityMismatches_RetainsExactPublishedSnapshot()
    {
        var source = new PublicationMutableSource(CompositionTestData.Definition());
        using var catalog = new DefaultAgentDefinitionCatalog([source]);
        var previous = await catalog.RefreshAsync(TestContext.Current.CancellationToken);
        source.ReturnedSourceId = new AgentDefinitionSourceId("wrong");

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await catalog.RefreshAsync(TestContext.Current.CancellationToken));

        catalog.CurrentSnapshot.ShouldBeSameAs(previous);
    }

    [Fact]
    public async Task RefreshAsync_WhenSourceReturnsNull_RetainsExactPublishedSnapshotAndVersion()
    {
        var source = new PublicationMutableSource(CompositionTestData.Definition());
        using var catalog = new DefaultAgentDefinitionCatalog([source]);
        var previous = await catalog.RefreshAsync(TestContext.Current.CancellationToken);
        source.ReturnNull = true;

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await catalog.RefreshAsync(TestContext.Current.CancellationToken));

        catalog.CurrentSnapshot.ShouldBeSameAs(previous);
        catalog.CurrentSnapshot!.Version.ShouldBe(previous.Version);
    }

    [Fact]
    public void AgentDefinitionSourceSnapshot_WhenSourceIdIsDefault_ThrowsBeforeConstruction()
    {
        var exception = Should.Throw<ArgumentException>(() => new AgentDefinitionSourceSnapshot(
            default,
            new AgentDefinitionSourceVersion(1),
            0,
            []));

        exception.ParamName.ShouldBe("sourceId");
    }

    [Fact]
    public void AgentDefinitionSourceSnapshot_WhenValuesMatch_IsStructurallyEqual()
    {
        var definition = CompositionTestData.Definition();
        var first = new AgentDefinitionSourceSnapshot(
            new AgentDefinitionSourceId("source"),
            new AgentDefinitionSourceVersion(1),
            2,
            [definition]);
        var second = new AgentDefinitionSourceSnapshot(
            new AgentDefinitionSourceId("source"),
            new AgentDefinitionSourceVersion(1),
            2,
            [definition]);

        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }

    [Fact]
    public async Task RefreshAsync_WhenCanceledAfterSourceRead_DoesNotPublish()
    {
        var source = new PublicationMutableSource(CompositionTestData.Definition());
        using var catalog = new DefaultAgentDefinitionCatalog([source]);
        var previous = await catalog.RefreshAsync(TestContext.Current.CancellationToken);
        source.Definition = CompositionTestData.Definition(revision: 2);
        using var cancellation = new CancellationTokenSource();
        source.AfterRead = cancellation.Cancel;

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await catalog.RefreshAsync(cancellation.Token));

        catalog.CurrentSnapshot.ShouldBeSameAs(previous);
    }

    [Fact]
    public async Task RefreshAsync_WhenRemovedRevisionIsReintroducedWithDifferentContent_RejectsRebinding()
    {
        var source = new PublicationMutableSource(CompositionTestData.Definition());
        using var catalog = new DefaultAgentDefinitionCatalog([source]);
        var original = await catalog.RefreshAsync(TestContext.Current.CancellationToken);
        source.PublishEmpty = true;
        var removed = await catalog.RefreshAsync(TestContext.Current.CancellationToken);
        source.PublishEmpty = false;
        source.Definition = CompositionTestData.Definition(displayName: "rebound");

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await catalog.RefreshAsync(TestContext.Current.CancellationToken));

        catalog.CurrentSnapshot.ShouldBeSameAs(removed);
        catalog.CurrentSnapshot.ShouldNotBeSameAs(original);
    }

    [Fact]
    public async Task RefreshAsync_WhenSameRevisionChangesContent_RejectsAndKeepsExactPreviousSnapshotAndVersion()
    {
        var source = new PublicationMutableSource(CompositionTestData.Definition());
        using var catalog = new DefaultAgentDefinitionCatalog([source]);
        var previous = await catalog.GetSnapshotAsync(TestContext.Current.CancellationToken);
        source.Definition = CompositionTestData.Definition(displayName: "changed");

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await catalog.RefreshAsync(TestContext.Current.CancellationToken));

        catalog.CurrentSnapshot.ShouldBeSameAs(previous);
        catalog.CurrentSnapshot!.Version.ShouldBe(previous.Version);
        source.Definition = CompositionTestData.Definition(revision: 2);
        var next = await catalog.RefreshAsync(TestContext.Current.CancellationToken);
        next.Version.Value.ShouldBe(previous.Version.Value + 1);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenNoSources_ReturnsEmptyCatalog()
    {
        using var catalog = new DefaultAgentDefinitionCatalog([]);

        var snapshot = await catalog.GetSnapshotAsync(TestContext.Current.CancellationToken);

        snapshot.Definitions.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetSnapshotAsync_ComposesEverySourceInRegistrationOrder()
    {
        var first = CompositionTestData.Definition(new AgentId(Guid.NewGuid()), "first");
        var second = CompositionTestData.Definition(new AgentId(Guid.NewGuid()), "second");
        using var catalog = new DefaultAgentDefinitionCatalog([
            new FakeAgentDefinitionSource("a", 0, first),
            new FakeAgentDefinitionSource("b", 0, second),
        ]);

        var snapshot = await catalog.GetSnapshotAsync(TestContext.Current.CancellationToken);

        snapshot.Definitions.Select(definition => definition.DisplayName)
            .ShouldBe(["first", "second"]);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenCalledTwice_ReturnsTheSameImmutableSnapshot()
    {
        using var catalog = new DefaultAgentDefinitionCatalog([
            new FakeAgentDefinitionSource("a", 0, CompositionTestData.Definition()),
        ]);

        var first = await catalog.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var second = await catalog.GetSnapshotAsync(TestContext.Current.CancellationToken);

        second.ShouldBeSameAs(first);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenSourcesConflictAtDifferentPrecedence_HigherWins()
    {
        var baseline = CompositionTestData.Definition(displayName: "baseline");
        var overriding = CompositionTestData.Definition(displayName: "override");
        using var catalog = new DefaultAgentDefinitionCatalog([
            new FakeAgentDefinitionSource("baseline", 0, baseline),
            new FakeAgentDefinitionSource("override", 10, overriding),
        ]);

        var snapshot = await catalog.GetSnapshotAsync(TestContext.Current.CancellationToken);

        snapshot.Definitions.ShouldHaveSingleItem().DisplayName.ShouldBe("override");
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenLowerPrecedenceSourceIsReadLast_StillLoses()
    {
        var baseline = CompositionTestData.Definition(displayName: "baseline");
        var overriding = CompositionTestData.Definition(displayName: "override");
        using var catalog = new DefaultAgentDefinitionCatalog([
            new FakeAgentDefinitionSource("override", 10, overriding),
            new FakeAgentDefinitionSource("baseline", 0, baseline),
        ]);

        var snapshot = await catalog.GetSnapshotAsync(TestContext.Current.CancellationToken);

        snapshot.Definitions.ShouldHaveSingleItem().DisplayName.ShouldBe("override");
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenSourcesConflictAtEqualPrecedence_ThrowsInvalidOperationException()
    {
        var first = CompositionTestData.Definition(displayName: "first");
        var second = CompositionTestData.Definition(displayName: "second");
        using var catalog = new DefaultAgentDefinitionCatalog([
            new FakeAgentDefinitionSource("a", 0, first),
            new FakeAgentDefinitionSource("b", 0, second),
        ]);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await catalog.GetSnapshotAsync(TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("structurally identical");
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenSourcesAreStructurallyIdenticalAtEqualPrecedence_AllowsOneDefinition()
    {
        var definition = CompositionTestData.Definition();
        using var catalog = new DefaultAgentDefinitionCatalog([
            new FakeAgentDefinitionSource("a", 0, definition),
            new FakeAgentDefinitionSource("b", 0, definition),
        ]);

        var snapshot = await catalog.GetSnapshotAsync(TestContext.Current.CancellationToken);

        snapshot.Definitions.ShouldHaveSingleItem().ShouldBe(definition);
    }

    [Fact]
    public async Task ResolveAsync_WhenAgentExists_ReturnsResolvedDefinitionWithCatalogVersion()
    {
        using var catalog = new DefaultAgentDefinitionCatalog([
            new FakeAgentDefinitionSource("a", 0, CompositionTestData.Definition()),
        ]);

        var resolution = await catalog.ResolveAsync(
            CompositionTestData.AgentId,
            TestContext.Current.CancellationToken);

        var resolved = resolution.ShouldBeOfType<ResolvedAgentDefinition>();
        resolved.Definition.Id.ShouldBe(CompositionTestData.AgentId);
        resolved.CatalogVersion.Value.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ResolveAsync_WhenAgentIsUnknown_ReturnsNotFound()
    {
        using var catalog = new DefaultAgentDefinitionCatalog([]);
        var unknown = new AgentId(Guid.NewGuid());

        var resolution = await catalog.ResolveAsync(unknown, TestContext.Current.CancellationToken);

        resolution.ShouldBeOfType<AgentDefinitionNotFound>().AgentId.ShouldBe(unknown);
    }

    [Fact]
    public async Task RefreshAsync_PublishesAMonotonicallyIncreasingVersion()
    {
        using var catalog = new DefaultAgentDefinitionCatalog([
            new FakeAgentDefinitionSource("a", 0, CompositionTestData.Definition()),
        ]);

        var first = await catalog.RefreshAsync(TestContext.Current.CancellationToken);
        var second = await catalog.RefreshAsync(TestContext.Current.CancellationToken);

        second.Version.Value.ShouldBeGreaterThan(first.Version.Value);
    }

    [Fact]
    public void Constructor_WhenTwoSourcesShareAnId_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new DefaultAgentDefinitionCatalog([
            new FakeAgentDefinitionSource("dup", 0, CompositionTestData.Definition()),
            new FakeAgentDefinitionSource("dup", 1, CompositionTestData.Definition()),
        ]));

        exception.ParamName.ShouldBe("sources");
    }

    [Fact]
    public void Constructor_WhenSourcesIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new DefaultAgentDefinitionCatalog(null!));

        exception.ParamName.ShouldBe("sources");
    }

    [Fact]
    public void SupportsDynamicPublication_IsTrueForTheRefreshableFirstPartyCatalog()
    {
        using var catalog = new DefaultAgentDefinitionCatalog([]);

        catalog.SupportsDynamicPublication.ShouldBeTrue();
    }
}
