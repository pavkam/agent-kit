// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

/// <summary>Verifies <see cref="EmbeddingRuntimeReference"/> requires a complete selector, executor, and alias triple.</summary>
public sealed class EmbeddingRuntimeReferenceTests
{
    private static readonly EmbeddingSelectionPolicy _policy = new([new EmbeddingModelAlias("embed")]);

    [Fact]
    public void Constructor_WhenTripleIsComplete_PreservesEveryValue()
    {
        var reference = new EmbeddingRuntimeReference(new ComponentKey<IEmbeddingModelSelector>("s"), new ComponentKey<IEmbeddingRequestExecutor>("e"), _policy);

        reference.SelectorKey.Value.ShouldBe("s");
        reference.ExecutorKey.Value.ShouldBe("e");
        reference.Policy.ShouldBe(_policy);
    }

    [Fact]
    public void Constructor_WhenAKeyIsDefault_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new EmbeddingRuntimeReference(default, new ComponentKey<IEmbeddingRequestExecutor>("e"), _policy)).ParamName.ShouldBe("selectorKey");
        Should.Throw<ArgumentNullException>(() => new EmbeddingRuntimeReference(new ComponentKey<IEmbeddingModelSelector>("s"), default, _policy)).ParamName.ShouldBe("executorKey");
    }

    [Fact]
    public void Constructor_WhenPolicyIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new EmbeddingRuntimeReference(new ComponentKey<IEmbeddingModelSelector>("s"), new ComponentKey<IEmbeddingRequestExecutor>("e"), null!)).ParamName.ShouldBe("policy");

    [Fact]
    public void Constructor_WhenPolicyHasNoAliases_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new EmbeddingRuntimeReference(new ComponentKey<IEmbeddingModelSelector>("s"), new ComponentKey<IEmbeddingRequestExecutor>("e"), new EmbeddingSelectionPolicy([]))).ParamName.ShouldBe("policy");
}
