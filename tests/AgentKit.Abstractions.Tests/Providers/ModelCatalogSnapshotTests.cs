// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelCatalogSnapshot behavior and contracts.</summary>
public sealed class ModelCatalogSnapshotTests
{
    [Fact]
    public void Constructor_WhenConversationModelsContainNull_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new ModelCatalogSnapshot(new ModelCatalogVersion(1), [null!])).ParamName.ShouldBe("conversationModels");

    [Fact]
    public void Constructor_WhenConversationModelsContainDuplicateAlias_ThrowsExactArgumentException()
    {
        var model = ProvidersTestData.Descriptor();
        var exception = Should.Throw<ArgumentException>(() => new ModelCatalogSnapshot(new ModelCatalogVersion(1), [model, model]));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("conversationModels");
    }

    [Fact]
    public void Constructor_WhenConversationModelsIsEmpty_IsStructurallyValid()
    {
        var snapshot = new ModelCatalogSnapshot(new ModelCatalogVersion(1), []);
        snapshot.ConversationModels.ShouldBeEmpty();
    }

    [Fact]
    public void Initializer_WhenConversationModelsContainNull_ThrowsExactArgumentException()
    {
        var snapshot = Snapshot();
        Should.Throw<ArgumentException>(() => snapshot with { ConversationModels = [null!] }).ParamName.ShouldBe("ConversationModels");
    }

    [Fact]
    public void Initializer_WhenConversationModelsContainDuplicateAlias_ThrowsExactArgumentException()
    {
        var snapshot = Snapshot();
        var model = ProvidersTestData.Descriptor();
        Should.Throw<ArgumentException>(() => snapshot with { ConversationModels = [model, model] }).ParamName.ShouldBe("ConversationModels");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var snapshot = Snapshot();
        snapshot.Version.ShouldBe(new ModelCatalogVersion(1));
        snapshot.ConversationModels.ShouldBe([ProvidersTestData.Descriptor()]);
    }

    [Fact]
    public void FindConversationModel_WhenAliasMatches_ReturnsDescriptor()
    {
        var snapshot = Snapshot();
        snapshot.FindConversationModel(new ModelAlias("chat")).ShouldBe(ProvidersTestData.Descriptor());
    }

    [Fact]
    public void FindConversationModel_WhenAliasDoesNotMatch_ReturnsNull()
    {
        var snapshot = Snapshot();
        snapshot.FindConversationModel(new ModelAlias("missing")).ShouldBeNull();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Snapshot();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ModelCatalogSnapshot Snapshot() => new(new ModelCatalogVersion(1), [ProvidersTestData.Descriptor()]);
}
