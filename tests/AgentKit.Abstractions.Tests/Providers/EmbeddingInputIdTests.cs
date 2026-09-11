// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies EmbeddingInputId behavior and contracts.</summary>
public sealed class EmbeddingInputIdTests: Conformance.GuidIdentityConformanceTests<EmbeddingInputId>
{
    [Fact]
    public void EmbeddingInputId_Constructor_WhenEmptyGuid_ThrowsArgumentOutOfRangeException() => _ = Should.Throw<ArgumentOutOfRangeException>(() => new EmbeddingInputId(Guid.Empty));

    /// <inheritdoc/>
    protected override EmbeddingInputId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(EmbeddingInputId subject) => subject.Value;
}
