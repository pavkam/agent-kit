// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.InMemory.Tests;

/// <summary>Supplies isolated in-memory vector indexes to the shared conformance suite.</summary>
public sealed class InMemoryVectorIndexConformanceFixture: IVectorIndexConformanceFixture
{
    /// <summary>Initializes the isolated composition.</summary>
    public InMemoryVectorIndexConformanceFixture() => Index = CreateIndex(MemoryTestData.Space());

    /// <inheritdoc/>
    public ConformanceCapabilities Capabilities { get; } = new(supportsDurability: false);

    /// <inheritdoc/>
    public TestGoalGrants Grants { get; } = new();

    /// <inheritdoc/>
    public IVectorIndex Index { get; }

    /// <inheritdoc/>
    public IVectorIndex CreateIndex(VectorSpaceDescriptor space) =>
        new InMemoryVectorIndex(space, Grants, new TestIntentIds(), TimeProvider.System);

    /// <inheritdoc/>
    public ValueTask<IVectorIndex> ReopenAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Index);
}
