// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Is the complete persisted form of one <see cref="DocumentEntry"/>.</summary>
/// <param name="Tenant">The tenant partition text.</param>
/// <param name="Sequence">The store-wide creation sequence.</param>
/// <param name="Id">The document identity.</param>
/// <param name="AgentId">The owning agent.</param>
/// <param name="Owner">The owning principal text.</param>
/// <param name="Shared">Whether every principal in the tenant may read the document.</param>
/// <param name="Versions">Every stored version.</param>
/// <param name="Active">The active version text, or <see langword="null"/>.</param>
/// <param name="Receipts">The applied publications.</param>
/// <param name="ChunkIds">Every chunk identity of every version.</param>
/// <param name="DeletedAt">The logical deletion instant, or <see langword="null"/> while live.</param>
/// <param name="DeletionGeneration">The deletion generation, or <see langword="null"/> while live.</param>
/// <param name="Purged">Whether every version's chunks were physically removed.</param>
internal sealed record DocumentEntryDocument(
    string Tenant,
    long Sequence,
    Guid Id,
    Guid AgentId,
    string Owner,
    bool Shared,
    ImmutableArray<DocumentVersionDocument> Versions,
    string? Active,
    ImmutableArray<DocumentReceiptDocument> Receipts,
    ImmutableArray<Guid> ChunkIds,
    DateTimeOffset? DeletedAt,
    long? DeletionGeneration,
    bool Purged)
{
    /// <summary>Converts an entry to its persisted form.</summary>
    /// <param name="value">The non-null entry.</param>
    /// <returns>The document.</returns>
    internal static DocumentEntryDocument FromDomain(DocumentEntry value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            value.Tenant.Value,
            value.Sequence,
            value.Id.Value,
            value.AgentId.Value,
            value.Owner.Value,
            value.Shared,
            [.. value.Versions.Select(DocumentVersionDocument.FromDomain)],
            value.Active?.Value,
            [.. value.Receipts.Select(static receipt => new DocumentReceiptDocument(receipt.Key, receipt.Fingerprint, receipt.Version.Value))],
            [.. value.ChunkIds.Select(static chunk => chunk.Value)],
            value.Deletion?.DeletedAt,
            value.Deletion?.Generation,
            value.Deletion?.Purged ?? false);
    }

    /// <summary>Restores the entry, re-running every validation.</summary>
    /// <returns>The entry.</returns>
    /// <exception cref="ArgumentException">The document is internally inconsistent.</exception>
    internal DocumentEntry ToDomain()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(Owner);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Sequence);
        var versions = Versions.IsDefault ? [] : Versions;
        var receipts = Receipts.IsDefault ? [] : Receipts;
        var chunkIds = ChunkIds.IsDefault ? [] : ChunkIds;
        ArgumentException.ThrowIfContainsNull(versions, nameof(Versions));
        ArgumentException.ThrowIfContainsNull(receipts, nameof(Receipts));
        ArgumentException.ThrowIfNotEqual(DeletedAt is null, DeletionGeneration is null, nameof(DeletedAt));

        return new(
            new TenantId(Tenant),
            Sequence,
            new DocumentId(Id),
            new AgentId(AgentId),
            new PrincipalId(Owner),
            Shared,
            [.. versions.Select(static version => version.ToDomain())],
            Active is null ? null : new DocumentVersion(Active),
            [.. receipts.Select(static receipt => new DocumentWriteReceipt(receipt.Key, receipt.Fingerprint, new DocumentVersion(receipt.Version)))],
            [.. chunkIds.Select(static chunk => new ChunkId(chunk))],
            DeletedAt is { } at && DeletionGeneration is { } generation ? new DocumentDeletion(at, generation, Purged) : null);
    }
}
