// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Gives a retrieval source the exact stores the selected memory profile captured for this operation.</summary>
/// <remarks>Sources receive stores through the request rather than resolving keyed services, so a source can never combine one profile's retrieval with another agent's stores. The stores are borrowed for the call and must not be retained.</remarks>
public sealed record RetrievalSourceStores
{
    /// <summary>Initializes the store set.</summary>
    /// <param name="memoryStore">The profile's durable-memory store, or <see langword="null"/> when the profile has none.</param>
    /// <param name="documentStore">The profile's document store, or <see langword="null"/> when the profile has none.</param>
    /// <param name="vectorIndexes">The profile's vector indexes, or default for none.</param>
    /// <exception cref="ArgumentException"><paramref name="vectorIndexes"/> contains null.</exception>
    public RetrievalSourceStores(IMemoryStore? memoryStore, IDocumentStore? documentStore, ImmutableArray<IVectorIndex> vectorIndexes)
    {
        var declared = vectorIndexes.IsDefault ? [] : vectorIndexes;
        ArgumentException.ThrowIfContainsNull(declared, nameof(vectorIndexes));
        MemoryStore = memoryStore;
        DocumentStore = documentStore;
        VectorIndexes = declared;
    }

    /// <summary>Gets the profile's durable-memory store, or <see langword="null"/>.</summary>
    public IMemoryStore? MemoryStore { get; }

    /// <summary>Gets the profile's document store, or <see langword="null"/>.</summary>
    public IDocumentStore? DocumentStore { get; }

    /// <summary>Gets the profile's vector indexes.</summary>
    public ImmutableArray<IVectorIndex> VectorIndexes { get; }
}
