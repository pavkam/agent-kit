// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies EmbeddingItemSucceeded behavior and contracts.</summary>
public sealed class EmbeddingItemSucceededTests
{
    [Fact]
    public void EmbeddingItemSucceeded_Constructor_WhenInputIndexNegative_ThrowsArgumentOutOfRangeException() => _ = Should.Throw<ArgumentOutOfRangeException>(() => new EmbeddingItemSucceeded(-1, null, new DenseFloatVector([1.0f]), Space(), ExtensionData.Empty));
    [Fact]
    public void EmbeddingItemSucceeded_Constructor_WhenVectorNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new EmbeddingItemSucceeded(0, null, null!, Space(), ExtensionData.Empty));
        exception.ParamName.ShouldBe("vector");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new EmbeddingItemSucceeded(0, null, new DenseFloatVector([1.0f]), Space(), ExtensionData.Empty);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ProviderResponseIdentity ProviderIdentity() => new(new ProviderId("openai"), null, new ApiFamilyId("openai"), new ModelId("text-embedding-3-small"), new ModelId("text-embedding-3-small"), null, null, null);
    private static EmbeddingSpaceIdentity Space() => new(ProviderIdentity(), 3, EmbeddingElementType.Float32, EmbeddingPurpose.Document, ExtensionData.Empty);
}
