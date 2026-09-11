// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies EmbeddingRequest behavior and contracts.</summary>
public sealed class EmbeddingRequestTests
{
    private static EmbeddingRequest CreateEmbeddingRequest() => new([new TextEmbeddingInput("hello world", null)], EmbeddingPurpose.Document, dimensions: null, encoding: null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty);
    [Fact]
    public void EmbeddingRequest_Constructor_WhenInputsEmpty_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => new EmbeddingRequest([], EmbeddingPurpose.Document, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty));
    [Fact]
    public void EmbeddingRequest_Constructor_WhenInputsContainsNull_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => new EmbeddingRequest([null!], EmbeddingPurpose.Document, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty));
    [Fact]
    public void EmbeddingRequest_Constructor_WhenDimensionsLessThanOne_ThrowsArgumentOutOfRangeException() => _ = Should.Throw<ArgumentOutOfRangeException>(() => new EmbeddingRequest([new TextEmbeddingInput("hi", null)], EmbeddingPurpose.Document, 0, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty));
    [Fact]
    public void EmbeddingRequest_Constructor_WhenExtensionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new EmbeddingRequest([new TextEmbeddingInput("hi", null)], EmbeddingPurpose.Document, null, null, EmbeddingTruncation.ProviderDefault, null!));
        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void EmbeddingRequest_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = CreateEmbeddingRequest();
        var second = CreateEmbeddingRequest();
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void EmbeddingRequest_Equality_WhenDifferentInputs_InstancesAreNotEqual()
    {
        var first = CreateEmbeddingRequest();
        var second = first with
        {
            Inputs = [new TextEmbeddingInput("different", null)]
        };
        first.ShouldNotBe(second);
    }
}
