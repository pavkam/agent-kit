// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

using System.Security.Cryptography;
using System.Text;

/// <summary>Plans document reads and mutations over any <see cref="IDocumentLookup"/>, so every adapter applies identical versioning, active-pointer, idempotency, and deletion rules.</summary>
/// <remarks>
/// Planning is side-effect free. A publication stores a complete version and, when asked, switches the active pointer in the same
/// returned entry, so a reader can never observe a mixed or stale-and-current set. Scoping is derived from the consumed grant, and
/// an item outside the authorized tenant, agent, or principal visibility is reported as not found.
/// </remarks>
internal static class DocumentPlanner
{
    /// <summary>Plans one version publication.</summary>
    /// <param name="lookup">The stored-state reader.</param>
    /// <param name="request">The write request, already authorized.</param>
    /// <returns>The stored or replayed version, or a typed refusal.</returns>
    internal static DocumentPlan<DocumentWriteResult> PlanWrite(IDocumentLookup lookup, DocumentWriteRequest request)
    {
        Debug.Assert(lookup is not null, "Adapters supply a lookup.");
        Debug.Assert(request is not null, "Adapters validate the request before planning it.");
        var (tenant, agent, principal) = Scope(request.Grant);
        var record = request.Record;
        if (record.TenantId != tenant || record.AgentId != agent || record.Visibility.OwnerPrincipalId != principal)
        {
            return Refuse(DocumentWriteResult.Rejected, MemoryStoreFailureKind.ScopeMismatch, "The document is not owned by the authorized tenant, agent, and principal.");
        }

        var existing = lookup.Find(tenant, record.Id);
        if (existing is not null && (existing.AgentId != agent || existing.Owner != principal))
        {
            return Refuse(DocumentWriteResult.Rejected, MemoryStoreFailureKind.ScopeMismatch, "The document is owned by another agent or principal.");
        }

        if (existing?.Deletion is not null)
        {
            return Refuse(DocumentWriteResult.Rejected, MemoryStoreFailureKind.InvalidTransition, "The document was deleted.");
        }

        var key = request.IdempotencyKey.Value;
        var fingerprint = Fingerprint(request);
        if (existing?.Receipts.FirstOrDefault(receipt => receipt.Key == key) is { } applied)
        {
            if (applied.Fingerprint != fingerprint)
            {
                return Refuse(DocumentWriteResult.Rejected, MemoryStoreFailureKind.IdempotencyConflict, "The publication idempotency key was reused with a different publication.");
            }

            var stored = existing.Versions.First(version => version.Record.Version == applied.Version);
            return new(DocumentWriteResult.Written(stored.Record, stored.State, null, replayed: true), null);
        }

        if (existing?.Versions.Any(version => version.Record.Version == record.Version) == true)
        {
            return Refuse(DocumentWriteResult.Rejected, MemoryStoreFailureKind.IdempotencyConflict, "The document version is already published.");
        }

        var previous = request.Activate ? existing?.Active : null;
        var versions = (existing?.Versions ?? []).ToBuilder();
        if (previous is { } superseded)
        {
            for (var index = 0; index < versions.Count; index++)
            {
                if (versions[index].Record.Version == superseded)
                {
                    versions[index] = versions[index] with { State = DocumentVersionState.Superseded };
                }
            }
        }

        var state = request.Activate ? DocumentVersionState.Active : DocumentVersionState.Staged;
        versions.Add(new DocumentVersionEntry(record, request.Chunks, state));
        var entry = new DocumentEntry(
            tenant,
            existing?.Sequence ?? lookup.NextSequence,
            record.Id,
            agent,
            principal,
            record.Visibility.SharedWithTenant,
            versions.ToImmutable(),
            request.Activate ? record.Version : existing?.Active,
            (existing?.Receipts ?? []).Add(new DocumentWriteReceipt(key, fingerprint, record.Version)),
            [.. (existing?.ChunkIds ?? []).Concat(request.Chunks.Select(static chunk => chunk.Id)).Distinct()],
            null);
        return new(DocumentWriteResult.Written(record, state, previous, replayed: false), entry);
    }

    /// <summary>Plans one active-version switch.</summary>
    /// <param name="lookup">The stored-state reader.</param>
    /// <param name="request">The activation request, already authorized.</param>
    /// <returns>The newly active version, or a typed refusal.</returns>
    internal static DocumentPlan<DocumentActivateResult> PlanActivate(IDocumentLookup lookup, DocumentActivateRequest request)
    {
        Debug.Assert(lookup is not null, "Adapters supply a lookup.");
        Debug.Assert(request is not null, "Adapters validate the request before planning it.");
        var (tenant, agent, principal) = Scope(request.Grant);
        var entry = lookup.Find(tenant, request.Id);
        if (entry is null || entry.AgentId != agent || entry.Owner != principal)
        {
            return Refuse(DocumentActivateResult.Rejected, MemoryStoreFailureKind.NotFound, "The document does not exist.");
        }

        if (entry.Deletion is not null)
        {
            return Refuse(DocumentActivateResult.Rejected, MemoryStoreFailureKind.InvalidTransition, "The document was deleted.");
        }

        var target = entry.Versions.FirstOrDefault(version => version.Record.Version == request.Version);
        if (target is null)
        {
            return Refuse(DocumentActivateResult.Rejected, MemoryStoreFailureKind.NotFound, "The document version was never published.");
        }

        if (entry.Active == request.Version)
        {
            return new(DocumentActivateResult.Activated(target.Record, null, replayed: true), null);
        }

        if (entry.Active != request.ExpectedActiveVersion)
        {
            return Refuse(DocumentActivateResult.Rejected, MemoryStoreFailureKind.VersionConflict, "The expected active version does not match the stored pointer.");
        }

        var previous = entry.Active;
        var versions = entry.Versions.Select(version =>
            version.Record.Version == request.Version ? version with { State = DocumentVersionState.Active }
            : version.Record.Version == previous ? version with { State = DocumentVersionState.Superseded }
            : version).ToImmutableArray();
        return new(DocumentActivateResult.Activated(target.Record, previous, replayed: false), entry with { Versions = versions, Active = request.Version });
    }

    /// <summary>Reads one document version through the scope a grant names.</summary>
    /// <param name="lookup">The stored-state reader.</param>
    /// <param name="request">The read request, already authorized.</param>
    /// <param name="storeName">The adapter name a not-yet-purged tombstone names as pending.</param>
    /// <returns>The version, a content-free tombstone, or a not-found refusal.</returns>
    internal static DocumentReadResult Read(IDocumentLookup lookup, DocumentReadRequest request, string storeName)
    {
        Debug.Assert(lookup is not null, "Adapters supply a lookup.");
        Debug.Assert(request is not null, "Adapters validate the request before reading.");
        var (tenant, agent, principal) = Scope(request.Grant);
        var entry = lookup.Find(tenant, request.Id);
        if (entry is null || entry.AgentId != agent || (entry.Owner != principal && !entry.Shared))
        {
            return DocumentReadResult.Rejected(new MemoryStoreFailure(MemoryStoreFailureKind.NotFound, "The document does not exist."));
        }

        if (entry.Deletion is { } deletion)
        {
            return DocumentReadResult.Tombstoned(Receipt(entry, deletion, storeName));
        }

        var wanted = request.Version ?? entry.Active;
        var version = wanted is null ? null : entry.Versions.FirstOrDefault(candidate => candidate.Record.Version == wanted);
        return version is null
            ? DocumentReadResult.Rejected(new MemoryStoreFailure(MemoryStoreFailureKind.NotFound, "The document version does not exist."))
            : DocumentReadResult.Found(version.Record, version.State, request.IncludeChunks ? version.Chunks : default, entry.Active);
    }

    /// <summary>Plans one document deletion.</summary>
    /// <param name="lookup">The stored-state reader.</param>
    /// <param name="request">The delete request, already authorized.</param>
    /// <param name="storeName">The adapter name a not-yet-purged receipt names as pending.</param>
    /// <returns>The recorded or replayed deletion, or a typed refusal.</returns>
    internal static DocumentPlan<DocumentDeleteResult> PlanDelete(IDocumentLookup lookup, DocumentDeleteRequest request, string storeName)
    {
        Debug.Assert(lookup is not null, "Adapters supply a lookup.");
        Debug.Assert(request is not null, "Adapters validate the request before planning it.");
        Debug.Assert(!string.IsNullOrWhiteSpace(storeName), "Adapters name themselves.");
        var (tenant, agent, principal) = Scope(request.Grant);
        var entry = lookup.Find(tenant, request.Id);
        if (entry is null || entry.AgentId != agent || entry.Owner != principal)
        {
            return Refuse(DocumentDeleteResult.Rejected, MemoryStoreFailureKind.NotFound, "The document does not exist.");
        }

        var purge = request.Mode == DocumentDeleteMode.Purge;
        if (entry.Deletion is { } existing)
        {
            if (!purge || existing.Purged)
            {
                return new(DocumentDeleteResult.Deleted(Receipt(entry, existing, storeName), replayed: true), null);
            }

            var purgedEntry = Purged(entry with { Deletion = existing with { Purged = true } });
            return new(DocumentDeleteResult.Deleted(Receipt(purgedEntry, purgedEntry.Deletion!, storeName), replayed: false), purgedEntry);
        }

        var deletion = new DocumentDeletion(request.At, lookup.CurrentGeneration + 1, purge);
        var tombstoned = entry with { Deletion = deletion, Active = null };
        var stored = purge ? Purged(tombstoned) : tombstoned;
        return new(DocumentDeleteResult.Deleted(Receipt(stored, deletion, storeName), replayed: false), stored);
    }

    private static DocumentEntry Purged(DocumentEntry entry) => entry with
    {
        Versions = [.. entry.Versions.Select(static version => version with { Chunks = [] })],
        Receipts = [],
    };

    private static DocumentDeletionReceipt Receipt(DocumentEntry entry, DocumentDeletion deletion, string storeName) => new(
        entry.Id, logicallyDeleted: true, deletion.Purged, entry.ChunkIds, deletion.Purged ? [] : [storeName], deletion.DeletedAt, deletion.Generation);

    private static string Fingerprint(DocumentWriteRequest request)
    {
        var text = new StringBuilder()
            .Append(request.Record.Version.Value).Append('|')
            .Append(request.Record.ContentHash.Value).Append('|')
            .Append(request.Activate ? '1' : '0');
        foreach (var chunk in request.Chunks)
        {
            _ = text.Append('|').Append(chunk.Id.Value.ToString("N")).Append(':').Append(chunk.Hash.Value);
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static (TenantId Tenant, AgentId Agent, PrincipalId Principal) Scope(SecurityGrant grant) =>
        (grant.Identity.TenantId, grant.Scope.AgentId, grant.Identity.PrincipalId);

    private static DocumentPlan<TResult> Refuse<TResult>(Func<MemoryStoreFailure, TResult> reject, MemoryStoreFailureKind kind, string message) =>
        new(reject(new MemoryStoreFailure(kind, message)), null);
}
