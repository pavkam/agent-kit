// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies EmbeddingInput behavior and contracts.</summary>
public sealed class EmbeddingInputTests
{
    [Fact]
    public void EmbeddingInput_Hierarchy_LeafDerivesFromEmbeddingInput()
    {
        EmbeddingInput input = new TextEmbeddingInput("hello", null);
        _ = input.ShouldBeOfType<TextEmbeddingInput>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new TextEmbeddingInput("hello", null);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
