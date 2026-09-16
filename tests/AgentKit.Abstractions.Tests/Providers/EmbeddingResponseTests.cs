// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies EmbeddingResponse behavior and contracts.</summary>
public sealed class EmbeddingResponseTests
{
    [Fact]
    public void EmbeddingResponse_Constructor_WhenItemsEmpty_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => new EmbeddingResponse([], ModelUsage.NotReported, null, ExtensionData.Empty));
    [Fact]
    public void EmbeddingResponse_Constructor_WhenUsageNull_ThrowsArgumentNullException()
    {
        var items = ImmutableArray.Create<EmbeddingItemOutcome>(SucceededItem());
        var exception = Should.Throw<ArgumentNullException>(() => new EmbeddingResponse(items, null!, null, ExtensionData.Empty));
        exception.ParamName.ShouldBe("usage");
    }

    [Fact]
    public void EmbeddingResponse_Equality_WhenSameValues_InstancesAreEqual()
    {
        var items = ImmutableArray.Create<EmbeddingItemOutcome>(SucceededItem());
        var first = new EmbeddingResponse(items, ModelUsage.NotReported, null, ExtensionData.Empty);
        var second = new EmbeddingResponse(items, ModelUsage.NotReported, null, ExtensionData.Empty);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var items = ImmutableArray.Create<EmbeddingItemOutcome>(SucceededItem());
        var original = new EmbeddingResponse(items, ModelUsage.NotReported, null, ExtensionData.Empty);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ProviderResponseIdentity ProviderIdentity() => new(new ProviderId("openai"), null, new ApiFamilyId("openai"), new ModelId("text-embedding-3-small"), new ModelId("text-embedding-3-small"), null, null, null);
    private static EmbeddingSpaceIdentity Space() => new(ProviderIdentity(), 3, EmbeddingElementType.Float32, EmbeddingPurpose.Document, ExtensionData.Empty);
    private static EmbeddingItemSucceeded SucceededItem() => new(0, null, new DenseFloatVector([1.0f, 2.0f, 3.0f]), Space(), ExtensionData.Empty);
}
