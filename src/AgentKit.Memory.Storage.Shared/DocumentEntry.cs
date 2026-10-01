// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Is one stored document aggregate: every version, the active-version pointer, publication receipts, and any deletion evidence.</summary>
/// <remarks>The entry is immutable and is the unit every adapter persists. The active-version pointer is part of the entry, so a switch is atomic with the state change it describes.</remarks>
/// <param name="Tenant">The tenant partition.</param>
/// <param name="Sequence">The store-wide creation sequence.</param>
/// <param name="Id">The document identity.</param>
/// <param name="AgentId">The owning agent.</param>
/// <param name="Owner">The owning principal.</param>
/// <param name="Shared">Whether every principal in the tenant may read the document.</param>
/// <param name="Versions">Every stored version in publication order.</param>
/// <param name="Active">The currently active version, or <see langword="null"/> when none is active.</param>
/// <param name="Receipts">The applied publications in order.</param>
/// <param name="ChunkIds">Every chunk identity of every version, kept so a deletion receipt can drive index cleanup even after a purge.</param>
/// <param name="Deletion">The deletion evidence, or <see langword="null"/> while the document is live.</param>
internal sealed record DocumentEntry(
    TenantId Tenant,
    long Sequence,
    DocumentId Id,
    AgentId AgentId,
    PrincipalId Owner,
    bool Shared,
    ImmutableArray<DocumentVersionEntry> Versions,
    DocumentVersion? Active,
    ImmutableArray<DocumentWriteReceipt> Receipts,
    ImmutableArray<ChunkId> ChunkIds,
    DocumentDeletion? Deletion);
