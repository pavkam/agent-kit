// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Searches the profile's vector indexes and resolves each hit to its current document chunk.</summary>
/// <remarks>
/// <para>
/// The source needs the query embedded. It searches only vector indexes whose embedding space is the same vector space as the
/// query's, so a dimension match alone never mixes spaces; with no compatible index it fails with a typed embedding-unavailable
/// failure before any index is contacted. Every hit is resolved against the authoritative document store: a hit whose document has
/// no active version, or whose chunk is not in the active version's chunk set, is stale and is dropped, so stale and current chunks
/// are never returned as one source.
/// </para>
/// <para>The source reads only through the stores the profile captured and handed it, asking the security authority the query's authorization names for one single-use grant per store call. It is stateless and thread-safe. Register it with <see cref="Key"/> and select that key in a profile.</para>
/// </remarks>
/// <param name="authorities">The selector that resolves the authority a captured authorization names.</param>
/// <param name="grants">The issuer of single-use grants from the captured authority.</param>
internal sealed class DocumentChunkRetrievalSource(ISecurityAuthoritySelector authorities, MemoryGrantIssuer grants): IRetrievalSource
{
    /// <summary>Gets the key the source is registered and selected under.</summary>
    internal static RetrievalSourceKey Key { get; } = new("agentkit.memory.documents");

    /// <inheritdoc/>
    public RetrievalSourceDescriptor Descriptor { get; } = new(Key, "1", requiresEmbedding: true, new ComponentId("agentkit.memory.document-source"));

    /// <inheritdoc/>
    public async ValueTask<RetrievalSourceResult> SearchAsync(RetrievalSourceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Stores.DocumentStore is not { } documents)
        {
            return RetrievalSourceResult.Failed(new RetrievalFailure(RetrievalFailureKind.SourcesUnavailable, "The profile names no document store."));
        }

        if (request.Embedding is not { } embedding)
        {
            return RetrievalSourceResult.Failed(new RetrievalFailure(RetrievalFailureKind.EmbeddingUnavailable, "No query embedding is available."));
        }

        var indexes = request.Stores.VectorIndexes.Where(index => index.VectorSpace.EmbeddingSpace.IsSameVectorSpaceAs(embedding.Space)).ToImmutableArray();
        if (indexes.IsEmpty)
        {
            return RetrievalSourceResult.Failed(new RetrievalFailure(RetrievalFailureKind.EmbeddingUnavailable, "No vector index shares the query embedding's vector space."));
        }

        var authorization = request.Query.Context.Authorization;
        var matches = new List<(VectorMatch Match, int Order)>();
        long? watermark = null;
        for (var order = 0; order < indexes.Length; order++)
        {
            var index = indexes[order];
            var space = new VectorSpaceDescriptor(index.VectorSpace.IndexKey, embedding.Space, index.VectorSpace.DistanceMetric);
            var issue = await grants.IssueAsync(
                authorities, authorization, index.SecurityAudience, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                [VectorSecurityBinding.Resource(index.VectorSpace.IndexKey)],
                VectorSecurityBinding.SearchFingerprint(space, embedding.Vector, request.Limit, request.Query.Scope.Documents), cancellationToken).ConfigureAwait(false);
            if (issue.Grant is null)
            {
                return RetrievalSourceResult.Failed(new RetrievalFailure(RetrievalFailureKind.Denied, issue.SafeMessage ?? "The vector search was not authorized."));
            }

            var searched = await index.SearchAsync(
                new VectorSearchRequest(space, embedding.Vector, Math.Min(request.Limit, VectorSearchRequest.MaximumTopK), request.Query.Scope.Documents, issue.Grant), cancellationToken).ConfigureAwait(false);
            if (!searched.IsSearched)
            {
                return RetrievalSourceResult.Failed(new RetrievalFailure(RetrievalFailureKind.SourcesUnavailable, searched.Failure.SafeMessage));
            }

            watermark = Math.Max(watermark ?? 0, searched.Watermark);
            matches.AddRange(searched.Matches.Select(match => (match, order)));
        }

        var candidates = ImmutableArray.CreateBuilder<RetrievalCandidate>();
        var resolved = new Dictionary<DocumentId, DocumentReadResult>();
        foreach (var (match, _) in matches.OrderByDescending(static item => item.Match.Score).ThenBy(static item => item.Order).ThenBy(static item => item.Match.ChunkId.Value))
        {
            if (!resolved.TryGetValue(match.DocumentId, out var document))
            {
                var issue = await grants.IssueAsync(
                    authorities, authorization, documents.Descriptor.SecurityAudience, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                    [DocumentSecurityBinding.Resource(match.DocumentId)], DocumentSecurityBinding.ReadFingerprint(match.DocumentId, null, true), cancellationToken).ConfigureAwait(false);
                document = issue.Grant is null
                    ? DocumentReadResult.Rejected(new MemoryStoreFailure(MemoryStoreFailureKind.Denied, "The document read was not authorized."))
                    : await documents.ReadAsync(new DocumentReadRequest(match.DocumentId, null, true, issue.Grant), cancellationToken).ConfigureAwait(false);
                resolved[match.DocumentId] = document;
            }

            if (!document.IsFound || document.State != DocumentVersionState.Active || document.Record.Version != match.DocumentVersion)
            {
                continue;
            }

            var chunk = document.Chunks.FirstOrDefault(candidate => candidate.Id == match.ChunkId);
            if (chunk is null || chunk.Hash != match.SourceHash)
            {
                continue;
            }

            candidates.Add(new RetrievalCandidate(
                request.Query.Id,
                new RetrievalSourceIdentity(Key, Descriptor.Version, watermark),
                null,
                document.Record.Id,
                chunk.Id,
                new CandidateContent(chunk.Text),
                document.Record.Provenance,
                TrustClassification.UntrustedData,
                match.Score,
                document.Record.Classification));
            if (candidates.Count >= request.Limit)
            {
                break;
            }
        }

        return RetrievalSourceResult.Succeeded(candidates.ToImmutable(), null);
    }
}
