// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

using System.Globalization;

/// <summary>Plans memory reads and mutations over any <see cref="IMemoryLookup"/>, so every adapter applies identical scoping, idempotency, versioning, lifecycle, and deletion rules.</summary>
/// <remarks>
/// Planning is side-effect free: it reads through the lookup and returns the entries to persist. Scoping is derived from the
/// consumed grant: the tenant partitions state, the scope's agent owns it, and the principal decides visibility. An item
/// outside that scope is always reported as not found so its existence is never revealed.
/// </remarks>
internal static class MemoryPlanner
{
    private const string _correctionKeyPrefix = "agentkit.correction:";

    /// <summary>Plans one record creation.</summary>
    /// <param name="lookup">The stored-state reader.</param>
    /// <param name="request">The write request, already authorized.</param>
    /// <returns>A created or replayed result, or a typed refusal.</returns>
    internal static MemoryPlan<MemoryWriteResult> PlanWrite(IMemoryLookup lookup, MemoryWriteRequest request)
    {
        Debug.Assert(lookup is not null, "Adapters supply a lookup.");
        Debug.Assert(request is not null, "Adapters validate the request before planning it.");
        var (tenant, agent, principal) = Scope(request.Grant);
        var record = request.Record;
        if (record.TenantId != tenant || record.AgentId != agent || record.Visibility.OwnerPrincipalId != principal)
        {
            return Refuse(MemoryWriteResult.Rejected, MemoryStoreFailureKind.ScopeMismatch, "The record is not owned by the authorized tenant, agent, and principal.");
        }

        var key = request.IdempotencyKey.Value;
        if (lookup.FindByCreateKey(tenant, key) is { } stored)
        {
            return stored.Deletion is not null
                ? Refuse(MemoryWriteResult.Rejected, MemoryStoreFailureKind.InvalidTransition, "The record was deleted.")
                : stored.Created == record
                    ? new(MemoryWriteResult.Written(stored.Created, replayed: true), [])
                    : Refuse(MemoryWriteResult.Rejected, MemoryStoreFailureKind.IdempotencyConflict, "The creation idempotency key was reused with a different record.");
        }

        if (lookup.Find(tenant, record.Id) is not null)
        {
            return Refuse(MemoryWriteResult.Rejected, MemoryStoreFailureKind.IdempotencyConflict, "The memory identity is already in use.");
        }

        var entry = new MemoryEntry(tenant, lookup.NextSequence, key, record, record, [], null);
        return new(MemoryWriteResult.Written(record, replayed: false), [entry]);
    }

    /// <summary>Reads one record through the scope a grant names.</summary>
    /// <param name="lookup">The stored-state reader.</param>
    /// <param name="request">The read request, already authorized.</param>
    /// <returns>The record, a content-free tombstone, or a not-found refusal.</returns>
    internal static MemoryReadResult Read(IMemoryLookup lookup, MemoryReadRequest request)
    {
        Debug.Assert(lookup is not null, "Adapters supply a lookup.");
        Debug.Assert(request is not null, "Adapters validate the request before reading.");
        var (tenant, agent, principal) = Scope(request.Grant);
        var entry = lookup.Find(tenant, request.Id);
        return entry is null || !Sees(entry.Current, agent, principal)
            ? MemoryReadResult.Rejected(new MemoryStoreFailure(MemoryStoreFailureKind.NotFound, "The memory does not exist."))
            : entry.Deletion is { } deletion
                ? MemoryReadResult.Tombstoned(Tombstone(entry, deletion))
                : MemoryReadResult.Found(entry.Current);
    }

    /// <summary>Reads one page of the authorized agent's live records.</summary>
    /// <param name="lookup">The stored-state reader.</param>
    /// <param name="request">The list request, already authorized.</param>
    /// <returns>A page with a cursor and the deletion generation.</returns>
    internal static MemoryListResult List(IMemoryLookup lookup, MemoryListRequest request)
    {
        Debug.Assert(lookup is not null, "Adapters supply a lookup.");
        Debug.Assert(request is not null, "Adapters validate the request before listing.");
        var (tenant, agent, principal) = Scope(request.Grant);
        var matched = new List<MemoryEntry>();
        foreach (var entry in lookup.Scan(tenant, agent, request.AfterSequence))
        {
            var record = entry.Current;
            if (entry.Deletion is not null
                || !Sees(record, agent, principal)
                || !request.States.Contains(record.State)
                || (request.Namespace is { } scope && record.Namespace != scope)
                || (!request.Terms.IsEmpty && !request.Terms.Any(term => record.Content.Text.Contains(term, StringComparison.OrdinalIgnoreCase))))
            {
                continue;
            }

            matched.Add(entry);
            if (matched.Count > request.Limit)
            {
                break;
            }
        }

        var more = matched.Count > request.Limit;
        var page = matched.Take(request.Limit).ToList();
        return MemoryListResult.Page(
            [.. page.Select(static entry => entry.Current)],
            more ? page[^1].Sequence : null,
            lookup.CurrentGeneration);
    }

    /// <summary>Plans one lifecycle transition.</summary>
    /// <param name="lookup">The stored-state reader.</param>
    /// <param name="request">The transition request, already authorized.</param>
    /// <returns>The applied or replayed transition, or a typed refusal.</returns>
    internal static MemoryPlan<MemoryTransitionResult> PlanTransition(IMemoryLookup lookup, MemoryTransitionRequest request)
    {
        Debug.Assert(lookup is not null, "Adapters supply a lookup.");
        Debug.Assert(request is not null, "Adapters validate the request before planning it.");
        var (tenant, agent, principal) = Scope(request.Grant);
        var entry = lookup.Find(tenant, request.Id);
        if (entry is null || !Owns(entry.Current, agent, principal))
        {
            return Refuse(MemoryTransitionResult.Rejected, MemoryStoreFailureKind.NotFound, "The memory does not exist.");
        }

        if (entry.Deletion is not null)
        {
            return Refuse(MemoryTransitionResult.Rejected, MemoryStoreFailureKind.InvalidTransition, "The memory was deleted.");
        }

        var key = request.IdempotencyKey.Value;
        if (entry.Receipts.FirstOrDefault(receipt => receipt.Key == key) is { } applied)
        {
            return applied.To == request.To && applied.Expected == request.ExpectedVersion && applied.ReplacementId == request.Replacement?.Id
                ? new(MemoryTransitionResult.Transitioned(applied.Result, applied.Replacement, replayed: true), [])
                : Refuse(MemoryTransitionResult.Rejected, MemoryStoreFailureKind.IdempotencyConflict, "The transition idempotency key was reused with a different transition.");
        }

        var current = entry.Current;
        if (current.Version != request.ExpectedVersion)
        {
            return Refuse(MemoryTransitionResult.Rejected, MemoryStoreFailureKind.VersionConflict, "The expected version does not match the stored record.");
        }

        if (!MemoryLifecycleTransitions.IsAllowed(current.State, request.To))
        {
            return Refuse(MemoryTransitionResult.Rejected, MemoryStoreFailureKind.InvalidTransition, "The lifecycle transition is not allowed from the stored state.");
        }

        var replacement = request.Replacement;
        if (replacement is not null)
        {
            if (replacement.TenantId != tenant || replacement.AgentId != agent || replacement.Visibility.OwnerPrincipalId != principal)
            {
                return Refuse(MemoryTransitionResult.Rejected, MemoryStoreFailureKind.ScopeMismatch, "The replacement is not owned by the authorized tenant, agent, and principal.");
            }

            if (lookup.Find(tenant, replacement.Id) is not null)
            {
                return Refuse(MemoryTransitionResult.Rejected, MemoryStoreFailureKind.IdempotencyConflict, "The replacement identity is already in use.");
            }
        }

        var updated = current.WithState(request.To, NextVersion(current.Version), request.At);
        var receipt = new MemoryTransitionReceipt(key, request.To, request.ExpectedVersion, replacement?.Id, updated, replacement);
        var changed = entry with { Current = updated, Receipts = entry.Receipts.Add(receipt) };
        var upserts = replacement is null
            ? ImmutableArray.Create(changed)
            : [changed, new MemoryEntry(tenant, lookup.NextSequence, $"{_correctionKeyPrefix}{key}:{replacement.Id}", replacement, replacement, [], null)];
        return new(MemoryTransitionResult.Transitioned(updated, replacement, replayed: false), upserts);
    }

    /// <summary>Plans one deletion.</summary>
    /// <param name="lookup">The stored-state reader.</param>
    /// <param name="request">The delete request, already authorized.</param>
    /// <param name="storeName">The adapter name a not-yet-purged receipt names as pending.</param>
    /// <returns>The recorded or replayed deletion, or a typed refusal.</returns>
    internal static MemoryPlan<MemoryDeleteResult> PlanDelete(IMemoryLookup lookup, MemoryDeleteRequest request, string storeName)
    {
        Debug.Assert(lookup is not null, "Adapters supply a lookup.");
        Debug.Assert(request is not null, "Adapters validate the request before planning it.");
        Debug.Assert(!string.IsNullOrWhiteSpace(storeName), "Adapters name themselves.");
        var (tenant, agent, principal) = Scope(request.Grant);
        var entry = lookup.Find(tenant, request.Id);
        if (entry is null || !Owns(entry.Current, agent, principal))
        {
            return Refuse(MemoryDeleteResult.Rejected, MemoryStoreFailureKind.NotFound, "The memory does not exist.");
        }

        var purge = request.Mode == MemoryDeleteMode.Purge;
        if (entry.Deletion is { } existing)
        {
            if (!purge || existing.Purged)
            {
                return new(MemoryDeleteResult.Deleted(Receipt(entry, existing, storeName), replayed: true), []);
            }

            var purgedEntry = Purged(entry with { Deletion = existing with { Purged = true } }, request.At);
            return new(MemoryDeleteResult.Deleted(Receipt(purgedEntry, purgedEntry.Deletion!, storeName), replayed: false), [purgedEntry]);
        }

        if (request.ExpectedVersion is { } expected && expected != entry.Current.Version)
        {
            return Refuse(MemoryDeleteResult.Rejected, MemoryStoreFailureKind.VersionConflict, "The expected version does not match the stored record.");
        }

        var deletion = new MemoryDeletion(request.At, lookup.CurrentGeneration + 1, purge);
        var tombstoned = entry with
        {
            Current = entry.Current.WithState(MemoryLifecycleState.Deleted, entry.Current.Version, request.At),
            Deletion = deletion,
        };
        var stored = purge ? Purged(tombstoned, request.At) : tombstoned;
        return new(MemoryDeleteResult.Deleted(Receipt(stored, deletion, storeName), replayed: false), [stored]);
    }

    private static MemoryEntry Purged(MemoryEntry entry, DateTimeOffset at) => entry with
    {
        Created = entry.Created.WithState(entry.Created.State, entry.Created.Version, entry.Created.UpdatedAt, MemoryContent.Purged),
        Current = entry.Current.WithState(MemoryLifecycleState.Deleted, entry.Current.Version, at, MemoryContent.Purged),
        Receipts = [],
    };

    private static MemoryDeletionReceipt Receipt(MemoryEntry entry, MemoryDeletion deletion, string storeName) => new(
        entry.Current.Id, logicallyDeleted: true, deletion.Purged, deletion.Purged ? [] : [storeName], deletion.DeletedAt, deletion.Generation);

    private static MemoryTombstone Tombstone(MemoryEntry entry, MemoryDeletion deletion) => new(
        entry.Current.Id, entry.Current.AgentId, entry.Tenant, entry.Current.Version, deletion.DeletedAt, deletion.Purged, deletion.Generation);

    private static VersionToken NextVersion(VersionToken current) =>
        new(long.TryParse(current.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? (number + 1).ToString(CultureInfo.InvariantCulture)
            : "1");

    private static (TenantId Tenant, AgentId Agent, PrincipalId Principal) Scope(SecurityGrant grant) =>
        (grant.Identity.TenantId, grant.Scope.AgentId, grant.Identity.PrincipalId);

    private static bool Sees(DurableMemoryRecord record, AgentId agent, PrincipalId principal) =>
        record.AgentId == agent && (record.Visibility.OwnerPrincipalId == principal || record.Visibility.SharedWithTenant);

    private static bool Owns(DurableMemoryRecord record, AgentId agent, PrincipalId principal) =>
        record.AgentId == agent && record.Visibility.OwnerPrincipalId == principal;

    private static MemoryPlan<TResult> Refuse<TResult>(Func<MemoryStoreFailure, TResult> reject, MemoryStoreFailureKind kind, string message) =>
        new(reject(new MemoryStoreFailure(kind, message)), []);
}
