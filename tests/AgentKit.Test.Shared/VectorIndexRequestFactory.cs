// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Builds vector-index requests whose grants bind exactly the operation each request performs.</summary>
/// <param name="grants">The grant harness shared with the index under test.</param>
/// <param name="audience">The index audience the grants must name.</param>
public sealed class VectorIndexRequestFactory(TestGoalGrants grants, ComponentId audience)
{
    /// <summary>Builds an exactly authorized upsert request.</summary>
    /// <param name="space">The vector space of the batch.</param>
    /// <param name="records">The batch.</param>
    /// <param name="authorization">The caller's authorization.</param>
    /// <param name="key">The replay key.</param>
    /// <returns>The request.</returns>
    public VectorUpsertRequest Upsert(VectorSpaceDescriptor space, ImmutableArray<VectorRecord> records, SecurityAuthorizationContext authorization, string key = "upsert-1") => new(
        space,
        records,
        new IdempotencyKey(key),
        grants.Issue(audience, authorization, SecurityOperationKind.StateMutation, SecurityEffect.CreateOrReplace, [VectorSecurityBinding.Resource(space.IndexKey)],
            VectorSecurityBinding.UpsertFingerprint(space, records, new IdempotencyKey(key))));

    /// <summary>Builds an exactly authorized search request.</summary>
    /// <param name="space">The vector space of the query.</param>
    /// <param name="query">The query vector.</param>
    /// <param name="topK">The number of matches.</param>
    /// <param name="authorization">The caller's authorization.</param>
    /// <param name="documents">The document restriction, or default.</param>
    /// <returns>The request.</returns>
    public VectorSearchRequest Search(VectorSpaceDescriptor space, ImmutableArray<float> query, int topK, SecurityAuthorizationContext authorization, ImmutableArray<DocumentId> documents = default) => new(
        space,
        query,
        topK,
        documents,
        grants.Issue(audience, authorization, SecurityOperationKind.StateRead, SecurityEffect.Observe, [VectorSecurityBinding.Resource(space.IndexKey)],
            VectorSecurityBinding.SearchFingerprint(space, query, topK, documents)));

    /// <summary>Builds an exactly authorized delete request.</summary>
    /// <param name="space">The vector space of the index.</param>
    /// <param name="chunkIds">The chunks to remove.</param>
    /// <param name="authorization">The caller's authorization.</param>
    /// <param name="key">The replay key.</param>
    /// <returns>The request.</returns>
    public VectorDeleteRequest Delete(VectorSpaceDescriptor space, ImmutableArray<ChunkId> chunkIds, SecurityAuthorizationContext authorization, string key = "delete-1") => new(
        space,
        chunkIds,
        new IdempotencyKey(key),
        grants.Issue(audience, authorization, SecurityOperationKind.StateMutation, SecurityEffect.Delete, [VectorSecurityBinding.Resource(space.IndexKey)],
            VectorSecurityBinding.DeleteFingerprint(space, chunkIds, new IdempotencyKey(key))));
}
