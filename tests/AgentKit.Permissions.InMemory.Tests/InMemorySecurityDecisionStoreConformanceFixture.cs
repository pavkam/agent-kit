// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.InMemory.Tests;

using AgentKit.Conformance;

/// <summary>Creates one in-memory decision store for each reusable contract case.</summary>
public sealed class InMemorySecurityDecisionStoreConformanceFixture: ISecurityDecisionStoreConformanceFixture
{
    private InMemorySecurityDecisionStore? _store;

    /// <inheritdoc/>
    public ConformanceCapabilities Capabilities { get; } = new(supportsDurability: false);

    /// <inheritdoc/>
    public ValueTask<ISecurityDecisionStore> CreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _store ??= new InMemorySecurityDecisionStore();
        return ValueTask.FromResult<ISecurityDecisionStore>(_store);
    }

    /// <inheritdoc/>
    public IReadOnlyList<SecurityDecision> ReadRecorded(ISecurityDecisionStore store) =>
        ((InMemorySecurityDecisionStore) store).Decisions;

    /// <inheritdoc/>
    public ValueTask<ISecurityDecisionStore> RecreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _store = new InMemorySecurityDecisionStore();
        return ValueTask.FromResult<ISecurityDecisionStore>(_store);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
