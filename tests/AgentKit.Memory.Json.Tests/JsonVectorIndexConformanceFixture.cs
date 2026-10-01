// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Json.Tests;

/// <summary>Supplies isolated JSON vector indexes to the shared conformance suite.</summary>
public sealed class JsonVectorIndexConformanceFixture: IVectorIndexConformanceFixture, IDisposable
{
    private readonly JsonMemoryTestRoot _root = new();
    private readonly List<JsonVectorIndex> _extra = [];
    private JsonVectorIndex _index;
    private int _created;

    /// <summary>Initializes the isolated composition.</summary>
    public JsonVectorIndexConformanceFixture() => _index = Open("default", MemoryTestData.Space());

    /// <inheritdoc/>
    public ConformanceCapabilities Capabilities { get; } = new(supportsDurability: true);

    /// <inheritdoc/>
    public TestGoalGrants Grants { get; } = new();

    /// <inheritdoc/>
    public IVectorIndex Index => _index;

    /// <inheritdoc/>
    public IVectorIndex CreateIndex(VectorSpaceDescriptor space)
    {
        var index = Open($"extra-{++_created}", space);
        _extra.Add(index);
        return index;
    }

    /// <inheritdoc/>
    public ValueTask<IVectorIndex> ReopenAsync(CancellationToken cancellationToken = default)
    {
        _index.Dispose();
        _index = Open("default", MemoryTestData.Space());
        return ValueTask.FromResult<IVectorIndex>(_index);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _index.Dispose();
        foreach (var index in _extra)
        {
            index.Dispose();
        }

        _root.Dispose();
    }

    private JsonVectorIndex Open(string name, VectorSpaceDescriptor space) =>
        new(space, _root.Target(name), JsonMemorySettings.CreateDefault(), Grants, new TestIntentIds(), TimeProvider.System);
}
