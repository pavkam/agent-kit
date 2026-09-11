// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies EmbeddingVector behavior and contracts.</summary>
public sealed class EmbeddingVectorTests
{
    [Fact]
    public void EmbeddingVector_Hierarchy_EveryLeafDerivesFromEmbeddingVector()
    {
        EmbeddingVector dense = new DenseFloatVector([1.0f]);
        EmbeddingVector quantized = new QuantizedByteVector([1], signed: true);
        EmbeddingVector packed = new PackedBinaryVector([1], signed: false);
        _ = dense.ShouldBeOfType<DenseFloatVector>();
        _ = quantized.ShouldBeOfType<QuantizedByteVector>();
        _ = packed.ShouldBeOfType<PackedBinaryVector>();
    }
}
