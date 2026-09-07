// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

/// <summary>
/// Exercises catalog composition, source precedence, snapshot stability, and
/// resolution outcomes.
/// </summary>
public sealed class DefaultAgentDefinitionCatalogTests
{
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
        using var catalog = new DefaultAgentDefinitionCatalog([
            new FakeAgentDefinitionSource("a", 0, CompositionTestData.Definition()),
            new FakeAgentDefinitionSource("b", 0, CompositionTestData.Definition()),
        ]);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await catalog.GetSnapshotAsync(TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("same precedence");
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
    public void SupportsDynamicPublication_IsFalseForTheFixedFirstPartyCatalog()
    {
        using var catalog = new DefaultAgentDefinitionCatalog([]);

        catalog.SupportsDynamicPublication.ShouldBeFalse();
    }
}
