// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies EmbeddingModelDescriptor behavior and contracts.</summary>
public sealed class EmbeddingModelDescriptorTests
{
    [Fact]
    public void EmbeddingModelDescriptor_Constructor_WhenCapabilitiesNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new EmbeddingModelDescriptor(new EmbeddingModelAlias("default"), new ProviderId("openai"), new ApiFamilyId("openai"), new ModelId("text-embedding-3-small"), null, null!, EmptyLimits(), null, ExtensionData.Empty));
        exception.ParamName.ShouldBe("capabilities");
    }

    [Fact]
    public void EmbeddingModelDescriptor_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = Descriptor();
        var second = Descriptor();
        first.ShouldBe(second);
    }

    private static EmbeddingLimits EmptyLimits() => new(null, null, null, null);
    private static EmbeddingCapabilities Capabilities() => new(true, true, true, true, true, ExtensionData.Empty);
    private static EmbeddingModelDescriptor Descriptor() => new(new EmbeddingModelAlias("default"), new ProviderId("openai"), new ApiFamilyId("openai"), new ModelId("text-embedding-3-small"), null, Capabilities(), EmptyLimits(), null, ExtensionData.Empty);
}
