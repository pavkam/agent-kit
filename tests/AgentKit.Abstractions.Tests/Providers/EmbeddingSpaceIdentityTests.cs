// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies EmbeddingSpaceIdentity behavior and contracts.</summary>
public sealed class EmbeddingSpaceIdentityTests
{
    [Fact]
    public void EmbeddingSpaceIdentity_Constructor_WhenProviderNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new EmbeddingSpaceIdentity(null!, 3, EmbeddingElementType.Float32, EmbeddingPurpose.Document, ExtensionData.Empty));
        exception.ParamName.ShouldBe("provider");
    }

    [Fact]
    public void EmbeddingSpaceIdentity_Constructor_WhenDimensionsLessThanOne_ThrowsArgumentOutOfRangeException() => _ = Should.Throw<ArgumentOutOfRangeException>(() => new EmbeddingSpaceIdentity(ProviderIdentity(), 0, EmbeddingElementType.Float32, EmbeddingPurpose.Document, ExtensionData.Empty));
    [Fact]
    public void EmbeddingSpaceIdentity_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = new EmbeddingSpaceIdentity(ProviderIdentity(), 3, EmbeddingElementType.Float32, EmbeddingPurpose.Document, ExtensionData.Empty);
        var second = new EmbeddingSpaceIdentity(ProviderIdentity(), 3, EmbeddingElementType.Float32, EmbeddingPurpose.Document, ExtensionData.Empty);
        first.ShouldBe(second);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new EmbeddingSpaceIdentity(ProviderIdentity(), 3, EmbeddingElementType.Float32, EmbeddingPurpose.Document, ExtensionData.Empty);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ProviderResponseIdentity ProviderIdentity() => new(new ProviderId("openai"), null, new ApiFamilyId("openai"), new ModelId("text-embedding-3-small"), new ModelId("text-embedding-3-small"), null, null, null);
}
