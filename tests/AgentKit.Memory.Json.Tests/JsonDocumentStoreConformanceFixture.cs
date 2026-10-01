// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Json.Tests;

/// <summary>Supplies an isolated JSON document store to the shared conformance suite.</summary>
public sealed class JsonDocumentStoreConformanceFixture: IDocumentStoreConformanceFixture, IDisposable
{
    private readonly JsonMemoryTestRoot _root = new();
    private JsonDocumentStore _store;

    /// <summary>Initializes the isolated composition.</summary>
    public JsonDocumentStoreConformanceFixture() => _store = Open();

    /// <inheritdoc/>
    public ConformanceCapabilities Capabilities { get; } = new(supportsDurability: true);

    /// <inheritdoc/>
    public TestGoalGrants Grants { get; } = new();

    /// <inheritdoc/>
    public IDocumentStore Store => _store;

    /// <inheritdoc/>
    public ValueTask<IDocumentStore> ReopenAsync(CancellationToken cancellationToken = default)
    {
        _store.Dispose();
        _store = Open();
        return ValueTask.FromResult<IDocumentStore>(_store);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _store.Dispose();
        _root.Dispose();
    }

    private JsonDocumentStore Open() => new(new DocumentStoreKey("documents"), _root.Target(), JsonMemorySettings.CreateDefault(), Grants, new TestIntentIds(), TimeProvider.System);
}
