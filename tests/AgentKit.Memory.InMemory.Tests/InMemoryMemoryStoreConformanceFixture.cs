// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.InMemory.Tests;

/// <summary>Supplies an isolated in-memory memory store to the shared conformance suite.</summary>
public sealed class InMemoryMemoryStoreConformanceFixture: IMemoryStoreConformanceFixture
{
    /// <summary>Initializes the isolated composition.</summary>
    public InMemoryMemoryStoreConformanceFixture() =>
        Store = new InMemoryMemoryStore(new MemoryStoreKey("memory"), Grants, new TestIntentIds(), TimeProvider.System);

    /// <inheritdoc/>
    public ConformanceCapabilities Capabilities { get; } = new(supportsDurability: false);

    /// <inheritdoc/>
    public TestGoalGrants Grants { get; } = new();

    /// <inheritdoc/>
    public IMemoryStore Store { get; }

    /// <inheritdoc/>
    public ValueTask<IMemoryStore> ReopenAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Store);
}
