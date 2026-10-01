// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects the exact keyed memory store a memory profile names.</summary>
/// <remarks>Selection is by exact key and never falls back to another store or to registration order. An unknown key is a typed rejection, not a default.</remarks>
public interface IMemoryStoreSelector
{
    /// <summary>Selects one store.</summary>
    /// <param name="request">The exact key to select.</param>
    /// <param name="cancellationToken">Cancels selection.</param>
    /// <returns>The store, or a rejection naming no content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<MemoryStoreSelectionResult> SelectAsync(MemoryStoreSelectionRequest request, CancellationToken cancellationToken = default);
}
