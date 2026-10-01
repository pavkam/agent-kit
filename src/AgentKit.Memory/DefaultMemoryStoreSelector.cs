// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Selects the memory store registered under one exact key.</summary>
/// <remarks>Selection is by exact key and never falls back to another store or to registration order. An unregistered key, or a store whose descriptor names another key, is a typed rejection.</remarks>
/// <param name="services">The container holding the keyed store registrations.</param>
internal sealed class DefaultMemoryStoreSelector(IServiceProvider services): IMemoryStoreSelector
{
    /// <inheritdoc/>
    public ValueTask<MemoryStoreSelectionResult> SelectAsync(MemoryStoreSelectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var store = services.GetKeyedService<IMemoryStore>(request.Key.Value);
        return ValueTask.FromResult(
            store is null
                ? MemoryStoreSelectionResult.Rejected("No memory store is registered under the requested key.")
                : store.Descriptor.Key != request.Key
                    ? MemoryStoreSelectionResult.Rejected("The registered memory store names a different key than it is registered under.")
                    : MemoryStoreSelectionResult.Selected(store));
    }
}
