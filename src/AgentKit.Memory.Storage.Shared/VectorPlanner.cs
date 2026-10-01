// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

using System.Security.Cryptography;
using System.Text;

/// <summary>Plans vector upserts, searches, and deletions over any <see cref="IVectorLookup"/>, so every adapter applies identical scoping, scoring, idempotency, and watermark rules.</summary>
/// <remarks>
/// Every method checks the complete vector-space descriptor against the index before touching any state, so an incompatible
/// request is refused without a read or write. Search is an exact scan, deterministic because ties break by chunk identity.
/// </remarks>
internal static class VectorPlanner
{
    /// <summary>Checks that a request's space is exactly the index's space.</summary>
    /// <param name="index">The space the index holds.</param>
    /// <param name="requested">The space the request names.</param>
    /// <returns>A refusal, or <see langword="null"/> when the spaces are compatible.</returns>
    internal static MemoryStoreFailure? CheckSpace(VectorSpaceDescriptor index, VectorSpaceDescriptor requested) =>
        index.IsCompatibleWith(requested)
            ? null
            : new MemoryStoreFailure(MemoryStoreFailureKind.IncompatibleVectorSpace, "The request's vector space does not match the index's vector space.");

    /// <summary>Plans one batch upsert.</summary>
    /// <param name="lookup">The stored-state reader.</param>
    /// <param name="request">The upsert request, already authorized and space-checked.</param>
    /// <returns>The stored or replayed batch, or a typed refusal.</returns>
    internal static VectorPlan<VectorUpsertResult> PlanUpsert(IVectorLookup lookup, VectorUpsertRequest request)
    {
        Debug.Assert(lookup is not null, "Adapters supply a lookup.");
        Debug.Assert(request is not null, "Adapters validate the request before planning it.");
        var tenant = request.Grant.Identity.TenantId;
        var agent = request.Grant.Scope.AgentId;
        if (request.Records.Any(record => record.AgentId != agent || record.Visibility.TenantId != tenant))
        {
            return Refuse(VectorUpsertResult.Rejected, MemoryStoreFailureKind.ScopeMismatch, "A vector is not owned by the authorized tenant and agent.");
        }

        var key = request.IdempotencyKey.Value;
        var fingerprint = Fingerprint(request);
        if (lookup.FindReceipt(tenant, key) is { } applied)
        {
            return applied.Fingerprint == fingerprint && !applied.IsDelete
                ? new(VectorUpsertResult.Succeeded(applied.Count, applied.Watermark, replayed: true), [], [], null, applied.Watermark)
                : Refuse(VectorUpsertResult.Rejected, MemoryStoreFailureKind.IdempotencyConflict, "The upsert idempotency key was reused with a different batch.");
        }

        var watermark = lookup.Watermark + 1;
        var receipt = new VectorReceipt(tenant, key, fingerprint, request.Records.Length, watermark, IsDelete: false);
        return new(
            VectorUpsertResult.Succeeded(request.Records.Length, watermark, replayed: false),
            [.. request.Records.Select(record => new VectorEntry(tenant, record))],
            [],
            receipt,
            watermark);
    }

    /// <summary>Plans one search.</summary>
    /// <param name="lookup">The stored-state reader.</param>
    /// <param name="space">The space the index holds.</param>
    /// <param name="request">The search request, already authorized and space-checked.</param>
    /// <returns>Ranked matches with the watermark.</returns>
    internal static VectorSearchResult Search(IVectorLookup lookup, VectorSpaceDescriptor space, VectorSearchRequest request)
    {
        Debug.Assert(lookup is not null, "Adapters supply a lookup.");
        Debug.Assert(request is not null, "Adapters validate the request before searching.");
        var tenant = request.Grant.Identity.TenantId;
        var agent = request.Grant.Scope.AgentId;
        var principal = request.Grant.Identity.PrincipalId;
        var documents = request.Documents.IsEmpty ? null : request.Documents.ToHashSet();
        var scored = new List<(VectorRecord Record, double Score)>();
        foreach (var entry in lookup.Scan(tenant, agent))
        {
            var record = entry.Record;
            if (record.AgentId != agent
                || (record.Visibility.OwnerPrincipalId != principal && !record.Visibility.SharedWithTenant)
                || (documents is not null && !documents.Contains(record.DocumentId)))
            {
                continue;
            }

            scored.Add((record, VectorScoring.Score(space.DistanceMetric, request.Query.AsSpan(), record.Vector.AsSpan())));
        }

        var matches = scored
            .OrderByDescending(static item => item.Score)
            .ThenBy(static item => item.Record.ChunkId.Value)
            .Take(request.TopK)
            .Select(static item => new VectorMatch(item.Record.ChunkId, item.Record.DocumentId, item.Record.DocumentVersion, item.Score, item.Record.SourceHash))
            .ToImmutableArray();
        return VectorSearchResult.Searched(matches, lookup.Watermark);
    }

    /// <summary>Plans one batch deletion.</summary>
    /// <param name="lookup">The stored-state reader.</param>
    /// <param name="request">The delete request, already authorized and space-checked.</param>
    /// <returns>The recorded or replayed deletion, or a typed refusal.</returns>
    internal static VectorPlan<VectorDeleteResult> PlanDelete(IVectorLookup lookup, VectorDeleteRequest request)
    {
        Debug.Assert(lookup is not null, "Adapters supply a lookup.");
        Debug.Assert(request is not null, "Adapters validate the request before planning it.");
        var tenant = request.Grant.Identity.TenantId;
        var agent = request.Grant.Scope.AgentId;
        var key = request.IdempotencyKey.Value;
        var fingerprint = Fingerprint(request);
        if (lookup.FindReceipt(tenant, key) is { } applied)
        {
            return applied.Fingerprint == fingerprint && applied.IsDelete
                ? new(VectorDeleteResult.Succeeded(applied.Count, applied.Watermark), [], [], null, applied.Watermark)
                : Refuse(VectorDeleteResult.Rejected, MemoryStoreFailureKind.IdempotencyConflict, "The deletion idempotency key was reused with a different batch.");
        }

        var present = request.ChunkIds
            .Distinct()
            .Where(chunk => lookup.Find(tenant, chunk) is { } entry && entry.Record.AgentId == agent)
            .ToImmutableArray();
        var watermark = present.IsEmpty ? lookup.Watermark : lookup.Watermark + 1;
        var receipt = new VectorReceipt(tenant, key, fingerprint, present.Length, watermark, IsDelete: true);
        return new(VectorDeleteResult.Succeeded(present.Length, watermark), [], present, receipt, watermark);
    }

    private static string Fingerprint(VectorUpsertRequest request)
    {
        var text = new StringBuilder("upsert");
        foreach (var record in request.Records)
        {
            _ = text.Append('|').Append(record.ChunkId.Value.ToString("N")).Append(':').Append(record.DocumentId.Value.ToString("N"))
                .Append(':').Append(record.DocumentVersion.Value).Append(':').Append(record.Visibility.OwnerPrincipalId.Value)
                .Append(':').Append(record.Visibility.SharedWithTenant ? '1' : '0').Append(':').Append(record.SourceHash.Value).Append(':');
            foreach (var component in record.Vector)
            {
                _ = text.Append(BitConverter.SingleToInt32Bits(component).ToString("x8", System.Globalization.CultureInfo.InvariantCulture)).Append(',');
            }
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static string Fingerprint(VectorDeleteRequest request) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            "delete|" + string.Join('|', request.ChunkIds.Select(static chunk => chunk.Value.ToString("N"))))));

    private static VectorPlan<TResult> Refuse<TResult>(Func<MemoryStoreFailure, TResult> reject, MemoryStoreFailureKind kind, string message) =>
        new(reject(new MemoryStoreFailure(kind, message)), [], [], null, 0);
}
