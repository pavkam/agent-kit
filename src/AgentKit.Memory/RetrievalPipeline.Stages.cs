// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

using System.Security.Cryptography;
using System.Text;

internal sealed partial class RetrievalPipeline
{
    private async ValueTask<RetrievalResult> RunAsync(RetrievalQuery original, IMemoryProfileRuntimeLease lease, CancellationToken cancellationToken)
    {
        var profile = lease.Profile;
        if (!profile.RetrievalEnabled)
        {
            return Fail(original, RetrievalFailureKind.RetrievalDisabled, "Retrieval is not enabled for the memory profile.");
        }

        if (original.MaximumClassification > profile.MaximumClassification)
        {
            return Fail(original, RetrievalFailureKind.ClassificationExceeded, "The query's classification ceiling exceeds the profile's ceiling.");
        }

        var effective = original.WithBudget(original.Budget.Narrow(profile.RetrievalBudget));
        var authorization = await _grants.IssueAsync(
            lease.SecurityAuthorities, original.Context.Authorization, _audience, SecurityOperationKind.StateRead, SecurityEffect.Observe,
            [new ProtectedResource(ProtectedResourceKind.ApplicationState, $"retrieval:{original.Id}")],
            Fingerprint("query", original.Id.ToString(), profile.ConfigurationFingerprint.Value, ((int) original.MaximumClassification).ToString(System.Globalization.CultureInfo.InvariantCulture),
                effective.Budget.MaximumItems.ToString(System.Globalization.CultureInfo.InvariantCulture), effective.Budget.MaximumBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                original.ExposureDestination?.ModelAlias.Value ?? string.Empty),
            cancellationToken).ConfigureAwait(false);
        if (authorization.Grant is null)
        {
            return Fail(original, authorization.IsUnavailable ? RetrievalFailureKind.ProfileUnavailable : RetrievalFailureKind.Denied, authorization.SafeMessage ?? "The query was not authorized.");
        }

        if (lease.QueryRewriter is { } rewriter)
        {
            var rewritten = await RewriteAsync(effective, rewriter, cancellationToken).ConfigureAwait(false);
            if (rewritten is null)
            {
                return Fail(original, RetrievalFailureKind.RewriteFailed, "The query could not be rewritten.");
            }

            effective = effective.WithQuery(rewritten);
        }

        var selection = await lease.Sources.SelectAsync(effective, cancellationToken).ConfigureAwait(false);
        if (!selection.IsSelected)
        {
            return Fail(original, RetrievalFailureKind.SourcesUnavailable, selection.SafeMessage);
        }

        var embedding = selection.Sources.Any(static source => source.Descriptor.RequiresEmbedding)
            ? await EmbedAsync(effective, lease, cancellationToken).ConfigureAwait(false)
            : null;
        var stores = new RetrievalSourceStores(lease.MemoryStore, lease.DocumentStore, lease.VectorIndexes);
        var searched = await SearchAsync(effective, lease, selection.Sources, embedding, stores, cancellationToken).ConfigureAwait(false);
        if (searched.Unavailable == selection.Sources.Length)
        {
            return Fail(original, RetrievalFailureKind.SourcesUnavailable, "Every selected retrieval source failed or was denied.");
        }

        var produced = searched.Candidates.Count;
        var current = new List<(RetrievalCandidate Candidate, int Order)>();
        var stale = 0;
        var unauthorized = 0;
        var documents = new Dictionary<DocumentId, DocumentReadResult>();
        foreach (var (candidate, order) in searched.Candidates)
        {
            if (candidate.Classification > effective.MaximumClassification)
            {
                unauthorized++;
            }
            else if (await IsCurrentAsync(effective, lease, candidate, documents, cancellationToken).ConfigureAwait(false))
            {
                current.Add((candidate, order));
            }
            else
            {
                stale++;
            }
        }

        var ranked = Deduplicate(current, out var duplicates);
        ranked = await RerankAsync(effective, lease, ranked, cancellationToken).ConfigureAwait(false);
        if (profile.RequireExposureAuthorization)
        {
            var (authorizedCandidates, exposureUnavailable) = await AuthorizeExposureAsync(effective, lease, ranked, cancellationToken).ConfigureAwait(false);
            if (exposureUnavailable)
            {
                return Fail(original, RetrievalFailureKind.ExposureUnavailable, "Exposure authorization could not be evaluated, so no candidate was exposed.");
            }

            unauthorized += ranked.Length - authorizedCandidates.Length;
            ranked = authorizedCandidates;
        }

        var budgeted = await lease.BudgetPolicy.SelectAsync(new RetrievalBudgetRequest(effective.Budget, ranked), cancellationToken).ConfigureAwait(false);
        var summary = new RetrievalSummary(produced, stale, unauthorized, duplicates, budgeted.Omitted, searched.Unavailable, searched.DeletionGeneration);
        var result = RetrievalResult.Completed(original.Id, budgeted.Selected, summary);
        var dispatch = await lease.Events.PublishAsync(
            profile.Key,
            new MemoryEvent(
                MemoryEventKind.RetrievalCompleted, original.Context.Identity.TenantId, original.Context.AgentId, original.Context.SessionId, profile.Key, profile.Version,
                null, null, original.Id, "completed", budgeted.Selected.Length, _time.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
        return dispatch.RequiredDeliveryComplete
            ? result
            : Fail(original, RetrievalFailureKind.ExposureUnavailable, "Required observation of the retrieval could not be recorded, so no candidate was exposed.");
    }

    private static RetrievalResult Fail(RetrievalQuery query, RetrievalFailureKind kind, string message) =>
        RetrievalResult.Failed(query.Id, new RetrievalFailure(kind, message));

    private static InputFingerprint Fingerprint(params string[] parts) =>
        new($"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f', parts))))}");

    private static async ValueTask<RetrievalQueryContent?> RewriteAsync(RetrievalQuery query, IQueryRewriter rewriter, CancellationToken cancellationToken)
    {
        QueryRewriteResult result;
        try
        {
            result = await rewriter.RewriteAsync(query, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }

        return result.IsRewritten
            && result.Content.RewrittenBy is { } by
            && by.Matches(rewriter.Descriptor)
            && string.Equals(result.Content.OriginalText, query.Query.OriginalText, StringComparison.Ordinal)
                ? result.Content
                : null;
    }

    private async ValueTask<RetrievalQueryEmbedding?> EmbedAsync(RetrievalQuery query, IMemoryProfileRuntimeLease lease, CancellationToken cancellationToken)
    {
        if (lease.Profile.Embedding is not { } reference || lease.EmbeddingSelector is not { } selector || lease.EmbeddingExecutor is not { } executor)
        {
            MemoryObservation.Safe(() => MemoryLog.EmbeddingUnavailable(_logger, query.Id, "not_configured"));
            return null;
        }

        using var scope = AgentKitActivityScope.Start(
            AgentKitActivityNames.RetrievalEmbed, ActivityKind.Internal, [new(AgentKitTagNames.RetrievalRequestId, query.Id.ToString())]);
        var grant = await _grants.IssueAsync(
            lease.SecurityAuthorities, query.Context.Authorization, _audience, SecurityOperationKind.StateRead, SecurityEffect.Egress,
            [new ProtectedResource(ProtectedResourceKind.ApplicationState, $"retrieval-embedding:{query.Id}")],
            Fingerprint("embedding", query.Id.ToString(), lease.Profile.ConfigurationFingerprint.Value), cancellationToken).ConfigureAwait(false);
        if (grant.Grant is null)
        {
            MemoryObservation.Safe(() => scope.Activity.SetFailed("denied", "denied"));
            MemoryObservation.Safe(() => MemoryLog.EmbeddingUnavailable(_logger, query.Id, "egress_denied"));
            return null;
        }

        try
        {
            var operation = Operation(query.Context);
            var selection = await selector.SelectAsync(
                new EmbeddingSelectionRequest(operation, reference.Policy, EmbeddingRequirements.None, lease.Models), cancellationToken).ConfigureAwait(false);
            if (selection is not EmbeddingModelSelected selected)
            {
                MemoryObservation.Safe(() => scope.Activity.SetFailed("no_model", "no_model"));
                MemoryObservation.Safe(() => MemoryLog.EmbeddingUnavailable(_logger, query.Id, "no_compatible_model"));
                return null;
            }

            var execution = await executor.ExecuteAsync(
                new EmbeddingExecutionRequest(
                    operation,
                    selected.Decision,
                    new EmbeddingRequest([new TextEmbeddingInput(query.Query.Text, null)], EmbeddingPurpose.Query, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty),
                    lease.Budget,
                    null,
                    SemanticOperationRetryPolicy.None),
                cancellationToken).ConfigureAwait(false);
            if (execution is EmbeddingExecutionCompleted { Response.Items: [EmbeddingItemSucceeded { Vector: DenseFloatVector vector } item] })
            {
                var embedding = new RetrievalQueryEmbedding(vector.Values, item.Space);
                MemoryObservation.Safe(() => scope.Activity.SetSuccessful("embedded"));
                return embedding;
            }

            MemoryObservation.Safe(() => scope.Activity.SetFailed("failed", "failed"));
            MemoryObservation.Safe(() => MemoryLog.EmbeddingUnavailable(_logger, query.Id, "execution_failed"));
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MemoryObservation.Safe(() => scope.Activity.SetFailed("faulted", exception.GetType().Name));
            MemoryObservation.Safe(() => MemoryLog.EmbeddingUnavailable(_logger, query.Id, "faulted"));
            return null;
        }
    }

    private static ProtectedSemanticOperationContext Operation(MemoryOperationContext context) =>
        new(context.AgentId, context.SessionId, null, context.Identity, context.Correlation, context.Authorization);

    private async ValueTask<SourceSearch> SearchAsync(
        RetrievalQuery query,
        IMemoryProfileRuntimeLease lease,
        ImmutableArray<IRetrievalSource> sources,
        RetrievalQueryEmbedding? embedding,
        RetrievalSourceStores stores,
        CancellationToken cancellationToken)
    {
        var tasks = sources.Select((source, order) => SearchOneAsync(query, lease, source, order, embedding, stores, cancellationToken)).ToArray();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        var candidates = new List<(RetrievalCandidate Candidate, int Order)>();
        long? generation = null;
        var unavailable = 0;
        foreach (var result in results)
        {
            if (result is null)
            {
                unavailable++;
                continue;
            }

            candidates.AddRange(result.Value.Candidates.Select(candidate => (candidate, result.Value.Order)));
            if (result.Value.DeletionGeneration is { } observed)
            {
                generation = Math.Max(generation ?? 0, observed);
            }
        }

        return new SourceSearch(candidates, unavailable, generation);
    }

    private async Task<(ImmutableArray<RetrievalCandidate> Candidates, int Order, long? DeletionGeneration)?> SearchOneAsync(
        RetrievalQuery query,
        IMemoryProfileRuntimeLease lease,
        IRetrievalSource source,
        int order,
        RetrievalQueryEmbedding? embedding,
        RetrievalSourceStores stores,
        CancellationToken cancellationToken)
    {
        var descriptor = source.Descriptor;
        using var scope = AgentKitActivityScope.Start(
            AgentKitActivityNames.RetrievalSourceSearch,
            ActivityKind.Internal,
            [new(AgentKitTagNames.RetrievalRequestId, query.Id.ToString()), new(AgentKitTagNames.RetrievalSourceKey, descriptor.Key.Value)]);
        var grant = await _grants.IssueAsync(
            lease.SecurityAuthorities, query.Context.Authorization, descriptor.SecurityAudience, SecurityOperationKind.StateRead, SecurityEffect.Observe,
            [new ProtectedResource(ProtectedResourceKind.ApplicationState, $"retrieval-source:{descriptor.Key.Value}:{query.Id}")],
            Fingerprint("source", query.Id.ToString(), descriptor.Key.Value, descriptor.Version), cancellationToken).ConfigureAwait(false);
        if (grant.Grant is null)
        {
            MemoryObservation.Safe(() => scope.Activity.SetFailed("denied", "denied"));
            MemoryObservation.Safe(() => MemoryLog.RetrievalSourceUnavailable(_logger, query.Id, descriptor.Key.Value, "denied"));
            return null;
        }

        try
        {
            var result = await source.SearchAsync(
                new RetrievalSourceRequest(query, embedding, query.Budget.MaximumItems, grant.Grant, stores), cancellationToken).ConfigureAwait(false);
            if (!result.IsSucceeded)
            {
                MemoryObservation.Safe(() => scope.Activity.SetFailed("failed", "failed"));
                MemoryObservation.Safe(() => MemoryLog.RetrievalSourceUnavailable(_logger, query.Id, descriptor.Key.Value, "failed"));
                return null;
            }

            MemoryObservation.Safe(() => scope.Activity.SetSuccessful("searched"));
            return (result.Candidates, order, result.DeletionGeneration);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MemoryObservation.Safe(() => scope.Activity.SetFailed("faulted", exception.GetType().Name));
            MemoryObservation.Safe(() => MemoryLog.RetrievalSourceUnavailable(_logger, query.Id, descriptor.Key.Value, "faulted"));
            return null;
        }
    }

    private async ValueTask<bool> IsCurrentAsync(
        RetrievalQuery query,
        IMemoryProfileRuntimeLease lease,
        RetrievalCandidate candidate,
        Dictionary<DocumentId, DocumentReadResult> documents,
        CancellationToken cancellationToken)
    {
        if (candidate.MemoryId is { } memoryId)
        {
            if (lease.MemoryStore is not { } store)
            {
                return false;
            }

            var grant = await _grants.IssueAsync(
                lease.SecurityAuthorities, query.Context.Authorization, store.Descriptor.SecurityAudience, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                [MemorySecurityBinding.Resource(memoryId)], MemorySecurityBinding.ReadFingerprint(memoryId), cancellationToken).ConfigureAwait(false);
            if (grant.Grant is null)
            {
                return false;
            }

            var read = await store.ReadAsync(new MemoryReadRequest(memoryId, grant.Grant), cancellationToken).ConfigureAwait(false);
            return read.IsFound
                && read.Record.State == MemoryLifecycleState.Active
                && !(read.Record.Retention.ExpiresAt is { } expiry && expiry <= _time.GetUtcNow())
                && string.Equals(read.Record.Content.Text, candidate.Content.Text, StringComparison.Ordinal);
        }

        var documentId = candidate.DocumentId!.Value;
        if (lease.DocumentStore is not { } documentStore)
        {
            return false;
        }

        if (!documents.TryGetValue(documentId, out var document))
        {
            var grant = await _grants.IssueAsync(
                lease.SecurityAuthorities, query.Context.Authorization, documentStore.Descriptor.SecurityAudience, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                [DocumentSecurityBinding.Resource(documentId)], DocumentSecurityBinding.ReadFingerprint(documentId, null, true), cancellationToken).ConfigureAwait(false);
            document = grant.Grant is null
                ? DocumentReadResult.Rejected(new MemoryStoreFailure(MemoryStoreFailureKind.Denied, "The document read was not authorized."))
                : await documentStore.ReadAsync(new DocumentReadRequest(documentId, null, true, grant.Grant), cancellationToken).ConfigureAwait(false);
            documents[documentId] = document;
        }

        return document.IsFound
            && document.State == DocumentVersionState.Active
            && (candidate.ChunkId is not { } chunkId || document.Chunks.Any(chunk => chunk.Id == chunkId && string.Equals(chunk.Text, candidate.Content.Text, StringComparison.Ordinal)));
    }

    private static ImmutableArray<RetrievalCandidate> Deduplicate(List<(RetrievalCandidate Candidate, int Order)> candidates, out int duplicates)
    {
        var ordered = candidates
            .OrderByDescending(static item => item.Candidate.Score)
            .ThenBy(static item => item.Order)
            .ThenBy(static item => IdentityKey(item.Candidate), StringComparer.Ordinal)
            .Select(static item => item.Candidate);
        var seenIdentity = new HashSet<string>(StringComparer.Ordinal);
        var seenText = new HashSet<string>(StringComparer.Ordinal);
        var kept = ImmutableArray.CreateBuilder<RetrievalCandidate>();
        duplicates = 0;
        foreach (var candidate in ordered)
        {
            var text = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(candidate.Content.Text.Trim().ToUpperInvariant())));
            if (!seenIdentity.Add(IdentityKey(candidate)) || !seenText.Add(text))
            {
                duplicates++;
                continue;
            }

            kept.Add(candidate);
        }

        return kept.ToImmutable();
    }

    private static string IdentityKey(RetrievalCandidate candidate) =>
        candidate.ChunkId is { } chunk ? $"chunk:{chunk}" : candidate.MemoryId is { } memory ? $"memory:{memory}" : $"document:{candidate.DocumentId}";

    private async ValueTask<ImmutableArray<RetrievalCandidate>> RerankAsync(
        RetrievalQuery query,
        IMemoryProfileRuntimeLease lease,
        ImmutableArray<RetrievalCandidate> candidates,
        CancellationToken cancellationToken)
    {
        if (lease.Profile.Reranker is not { } reference || lease.RerankerSelector is not { } selector || lease.RerankerExecutor is not { } executor || candidates.Length < 2)
        {
            return candidates;
        }

        using var scope = AgentKitActivityScope.Start(
            AgentKitActivityNames.RetrievalRerank, ActivityKind.Internal, [new(AgentKitTagNames.RetrievalRequestId, query.Id.ToString())]);
        try
        {
            var operation = Operation(query.Context);
            var selection = await selector.SelectAsync(
                new RerankerSelectionRequest(operation, reference.Policy, RerankerRequirements.None, lease.Models), cancellationToken).ConfigureAwait(false);
            if (selection is not RerankerSelected selected)
            {
                Degrade(scope, query, "no_compatible_reranker");
                return candidates;
            }

            var documents = candidates
                .Select(static (candidate, index) => new RerankDocument(
                    candidate.DocumentId ?? new DocumentId(candidate.MemoryId!.Value.Value), index, candidate.Content.Text, ExtensionData.Empty))
                .ToImmutableArray();
            var execution = await executor.ExecuteAsync(
                new RerankExecutionRequest(
                    operation, selected.Decision, new RerankRequest(query.Query.Text, documents, null, ProviderRequestOptions.Empty), lease.Budget, null, SemanticOperationRetryPolicy.None),
                cancellationToken).ConfigureAwait(false);
            if (execution is not RerankExecutionSucceeded succeeded)
            {
                Degrade(scope, query, "execution_failed");
                return candidates;
            }

            var reranked = ImmutableArray.CreateBuilder<RetrievalCandidate>();
            var used = new HashSet<int>();
            foreach (var result in succeeded.Response.Results.OrderByDescending(static item => item.RelevanceScore).ThenBy(static item => item.InputIndex))
            {
                if (result.InputIndex >= 0 && result.InputIndex < candidates.Length && used.Add(result.InputIndex) && double.IsFinite(result.RelevanceScore))
                {
                    reranked.Add(candidates[result.InputIndex].WithScore(result.RelevanceScore));
                }
            }

            for (var index = 0; index < candidates.Length; index++)
            {
                if (!used.Contains(index))
                {
                    reranked.Add(candidates[index]);
                }
            }

            MemoryObservation.Safe(() => scope.Activity.SetSuccessful("reranked"));
            return reranked.ToImmutable();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Degrade(scope, query, "faulted");
            return candidates;
        }
    }

    private void Degrade(AgentKitActivityScope scope, RetrievalQuery query, string reason)
    {
        MemoryObservation.Safe(() => scope.Activity.SetFailed(reason, reason));
        MemoryObservation.Safe(() => MemoryLog.RerankDegraded(_logger, query.Id, reason));
    }

    private async ValueTask<(ImmutableArray<RetrievalCandidate> Authorized, bool Unavailable)> AuthorizeExposureAsync(
        RetrievalQuery query,
        IMemoryProfileRuntimeLease lease,
        ImmutableArray<RetrievalCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var authorized = ImmutableArray.CreateBuilder<RetrievalCandidate>();
        foreach (var candidate in candidates)
        {
            var key = IdentityKey(candidate);
            var issue = await _grants.IssueAsync(
                lease.SecurityAuthorities, query.Context.Authorization, _audience, SecurityOperationKind.StateRead, SecurityEffect.Egress,
                [new ProtectedResource(ProtectedResourceKind.ApplicationState, $"retrieval-exposure:{query.Id}:{key}")],
                Fingerprint(
                    "exposure", query.Id.ToString(), key, candidate.Source.Key.Value, candidate.Source.Version,
                    candidate.Source.IndexWatermark?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                    ((int) candidate.Classification).ToString(System.Globalization.CultureInfo.InvariantCulture), query.ExposureDestination?.ModelAlias.Value ?? string.Empty),
                cancellationToken).ConfigureAwait(false);
            if (issue.IsUnavailable)
            {
                return (default, true);
            }

            if (issue.Grant is not null)
            {
                authorized.Add(candidate);
            }
        }

        return (authorized.ToImmutable(), false);
    }

    private sealed record SourceSearch(List<(RetrievalCandidate Candidate, int Order)> Candidates, int Unavailable, long? DeletionGeneration);
}
