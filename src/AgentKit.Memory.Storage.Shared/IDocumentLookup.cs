// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Gives the document planner read access to stored entries, however an adapter keeps them.</summary>
/// <remarks>The planner calls these members only while the adapter holds whatever gate or transaction makes the answers consistent with the write that follows.</remarks>
internal interface IDocumentLookup
{
    /// <summary>Gets the creation sequence the next new entry receives.</summary>
    /// <value>One greater than the highest stored creation sequence.</value>
    public long NextSequence { get; }

    /// <summary>Gets the highest deletion generation assigned so far.</summary>
    /// <value>Zero before any deletion.</value>
    public long CurrentGeneration { get; }

    /// <summary>Finds an entry by identity.</summary>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="id">The document identity.</param>
    /// <returns>The entry, or <see langword="null"/> when the tenant has none.</returns>
    public DocumentEntry? Find(TenantId tenant, DocumentId id);
}
