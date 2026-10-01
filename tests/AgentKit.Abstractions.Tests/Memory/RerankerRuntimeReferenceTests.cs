// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

/// <summary>Verifies <see cref="RerankerRuntimeReference"/> requires a complete selector, executor, and alias triple.</summary>
public sealed class RerankerRuntimeReferenceTests
{
    private static readonly RerankerSelectionPolicy _policy = new([new RerankerAlias("rerank")]);

    [Fact]
    public void Constructor_WhenTripleIsComplete_PreservesEveryValue()
    {
        var reference = new RerankerRuntimeReference(new ComponentKey<IRerankerSelector>("s"), new ComponentKey<IRerankRequestExecutor>("e"), _policy);

        reference.SelectorKey.Value.ShouldBe("s");
        reference.ExecutorKey.Value.ShouldBe("e");
        reference.Policy.ShouldBe(_policy);
    }

    [Fact]
    public void Constructor_WhenAKeyIsDefault_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new RerankerRuntimeReference(default, new ComponentKey<IRerankRequestExecutor>("e"), _policy)).ParamName.ShouldBe("selectorKey");
        Should.Throw<ArgumentNullException>(() => new RerankerRuntimeReference(new ComponentKey<IRerankerSelector>("s"), default, _policy)).ParamName.ShouldBe("executorKey");
    }

    [Fact]
    public void Constructor_WhenPolicyIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new RerankerRuntimeReference(new ComponentKey<IRerankerSelector>("s"), new ComponentKey<IRerankRequestExecutor>("e"), null!)).ParamName.ShouldBe("policy");

    [Fact]
    public void Constructor_WhenPolicyHasNoAliases_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new RerankerRuntimeReference(new ComponentKey<IRerankerSelector>("s"), new ComponentKey<IRerankRequestExecutor>("e"), new RerankerSelectionPolicy([]))).ParamName.ShouldBe("policy");
}
