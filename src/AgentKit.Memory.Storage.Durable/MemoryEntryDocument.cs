// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Is the complete persisted form of one <see cref="MemoryEntry"/>.</summary>
/// <param name="Tenant">The tenant partition text.</param>
/// <param name="Sequence">The store-wide creation sequence.</param>
/// <param name="CreateKey">The creation idempotency key.</param>
/// <param name="Created">The record as first written.</param>
/// <param name="Current">The record as it stands now.</param>
/// <param name="Receipts">The applied transitions.</param>
/// <param name="DeletedAt">The logical deletion instant, or <see langword="null"/> while live.</param>
/// <param name="DeletionGeneration">The deletion generation, or <see langword="null"/> while live.</param>
/// <param name="Purged">Whether the body was physically removed.</param>
internal sealed record MemoryEntryDocument(
    string Tenant,
    long Sequence,
    string CreateKey,
    MemoryRecordDocument Created,
    MemoryRecordDocument Current,
    ImmutableArray<MemoryReceiptDocument> Receipts,
    DateTimeOffset? DeletedAt,
    long? DeletionGeneration,
    bool Purged)
{
    /// <summary>Converts an entry to its persisted form.</summary>
    /// <param name="value">The non-null entry.</param>
    /// <returns>The document.</returns>
    internal static MemoryEntryDocument FromDomain(MemoryEntry value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            value.Tenant.Value,
            value.Sequence,
            value.CreateKey,
            MemoryRecordDocument.FromDomain(value.Created),
            MemoryRecordDocument.FromDomain(value.Current),
            [.. value.Receipts.Select(MemoryReceiptDocument.FromDomain)],
            value.Deletion?.DeletedAt,
            value.Deletion?.Generation,
            value.Deletion?.Purged ?? false);
    }

    /// <summary>Restores the entry, re-running every validation.</summary>
    /// <returns>The entry.</returns>
    /// <exception cref="ArgumentException">The document is internally inconsistent.</exception>
    internal MemoryEntry ToDomain()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(CreateKey);
        ArgumentNullException.ThrowIfNull(Created);
        ArgumentNullException.ThrowIfNull(Current);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Sequence);
        var receipts = Receipts.IsDefault ? [] : Receipts;
        ArgumentException.ThrowIfContainsNull(receipts, nameof(Receipts));
        ArgumentException.ThrowIfNotEqual(DeletedAt is null, DeletionGeneration is null, nameof(DeletedAt));

        return new(
            new TenantId(Tenant),
            Sequence,
            CreateKey,
            Created.ToDomain(),
            Current.ToDomain(),
            [.. receipts.Select(static receipt => receipt.ToDomain())],
            DeletedAt is { } at && DeletionGeneration is { } generation ? new MemoryDeletion(at, generation, Purged) : null);
    }
}
