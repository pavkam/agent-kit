// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelDescriptorSourceSnapshot behavior and contracts.</summary>
public sealed class ModelDescriptorSourceSnapshotTests
{
    [Fact]
    public void Constructor_WhenConversationModelsContainNull_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new ModelDescriptorSourceSnapshot(SourceId(), Version(), [null!])).ParamName.ShouldBe("conversationModels");

    [Fact]
    public void Constructor_WhenConversationModelsIsEmpty_IsStructurallyValid()
    {
        var snapshot = new ModelDescriptorSourceSnapshot(SourceId(), Version(), []);
        snapshot.ConversationModels.ShouldBeEmpty();
    }

    [Fact]
    public void Initializer_WhenConversationModelsContainNull_ThrowsExactArgumentException()
    {
        var snapshot = Snapshot();
        Should.Throw<ArgumentException>(() => snapshot with { ConversationModels = [null!] }).ParamName.ShouldBe("ConversationModels");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var snapshot = Snapshot();
        snapshot.SourceId.ShouldBe(SourceId());
        snapshot.Version.ShouldBe(Version());
        snapshot.ConversationModels.ShouldBe([ProvidersTestData.Descriptor()]);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Snapshot();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void Initializer_WhenConversationModelsAreValid_ReplacesValue()
    {
        var original = Snapshot();
        var replacement = ProvidersTestData.Descriptor("other");
        var copy = original with { ConversationModels = [replacement] };
        copy.ConversationModels.ShouldBe([replacement]);
    }

    private static ModelDescriptorSourceId SourceId() => new("source");
    private static ModelDescriptorSourceVersion Version() => new(1);
    private static ModelDescriptorSourceSnapshot Snapshot() => new(SourceId(), Version(), [ProvidersTestData.Descriptor()]);
}
