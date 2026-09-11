// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies EmbeddingModelAlias behavior and contracts.</summary>
public sealed class EmbeddingModelAliasTests: Conformance.StringIdentityConformanceTests<EmbeddingModelAlias>
{
    [Fact]
    public void EmbeddingModelAlias_Constructor_WhenValueIsWhitespace_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => new EmbeddingModelAlias(" "));
    [Fact]
    public void EmbeddingModelAlias_Equality_WhenSameValue_InstancesAreEqual() => new EmbeddingModelAlias("default").ShouldBe(new EmbeddingModelAlias("default"));

    /// <inheritdoc/>
    protected override EmbeddingModelAlias Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(EmbeddingModelAlias subject) => subject.Value;
}
