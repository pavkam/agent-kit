// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies TextEmbeddingInput behavior and contracts.</summary>
public sealed class TextEmbeddingInputTests
{
    [Fact]
    public void TextEmbeddingInput_Constructor_WhenTextNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new TextEmbeddingInput(null!, null));
        exception.ParamName.ShouldBe("text");
    }

    [Fact]
    public void TextEmbeddingInput_Equality_WhenSameValues_InstancesAreEqual() => new TextEmbeddingInput("hello", null).ShouldBe(new TextEmbeddingInput("hello", null));
}
