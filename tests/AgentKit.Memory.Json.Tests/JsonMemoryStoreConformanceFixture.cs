// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Json.Tests;

/// <summary>Supplies an isolated JSON memory store to the shared conformance suite.</summary>
public sealed class JsonMemoryStoreConformanceFixture: IMemoryStoreConformanceFixture, IDisposable
{
    private readonly JsonMemoryTestRoot _root = new();
    private JsonMemoryStore _store;

    /// <summary>Initializes the isolated composition.</summary>
    public JsonMemoryStoreConformanceFixture() => _store = Open();

    /// <inheritdoc/>
    public ConformanceCapabilities Capabilities { get; } = new(supportsDurability: true);

    /// <inheritdoc/>
    public TestGoalGrants Grants { get; } = new();

    /// <inheritdoc/>
    public IMemoryStore Store => _store;

    /// <inheritdoc/>
    public ValueTask<IMemoryStore> ReopenAsync(CancellationToken cancellationToken = default)
    {
        _store.Dispose();
        _store = Open();
        return ValueTask.FromResult<IMemoryStore>(_store);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _store.Dispose();
        _root.Dispose();
    }

    private JsonMemoryStore Open() => new(new MemoryStoreKey("memory"), _root.Target(), JsonMemorySettings.CreateDefault(), Grants, new TestIntentIds(), TimeProvider.System);
}
