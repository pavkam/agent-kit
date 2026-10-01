// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.InMemory.Tests;

/// <summary>Supplies an isolated in-memory document store to the shared conformance suite.</summary>
public sealed class InMemoryDocumentStoreConformanceFixture: IDocumentStoreConformanceFixture
{
    /// <summary>Initializes the isolated composition.</summary>
    public InMemoryDocumentStoreConformanceFixture() =>
        Store = new InMemoryDocumentStore(new DocumentStoreKey("documents"), Grants, new TestIntentIds(), TimeProvider.System);

    /// <inheritdoc/>
    public ConformanceCapabilities Capabilities { get; } = new(supportsDurability: false);

    /// <inheritdoc/>
    public TestGoalGrants Grants { get; } = new();

    /// <inheritdoc/>
    public IDocumentStore Store { get; }

    /// <inheritdoc/>
    public ValueTask<IDocumentStore> ReopenAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Store);
}
