// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Searches the profile's durable memory by case-insensitive keyword match over active records.</summary>
/// <remarks>
/// <para>
/// The source reads only through the memory store the profile captured and handed it, asking the security authority the query's
/// authorization names for one single-use grant per page. A record is a candidate only when it is active, unexpired, inside the
/// query's namespace scope, and visible to the authorized principal; a proposed, rejected, corrected, expired, or deleted record is
/// never retrieved. Candidates carry the record's provenance and classification and are untrusted data. The score is the fraction
/// of distinct query terms a record contains.
/// </para>
/// <para>The source is stateless and thread-safe. Register it with <see cref="Key"/> and select that key in a profile.</para>
/// </remarks>
/// <param name="authorities">The selector that resolves the authority a captured authorization names.</param>
/// <param name="grants">The issuer of single-use grants from the captured authority.</param>
/// <param name="time">The injected clock used to exclude expired records.</param>
internal sealed class DurableMemoryRetrievalSource(ISecurityAuthoritySelector authorities, MemoryGrantIssuer grants, TimeProvider time): IRetrievalSource
{
    private const int _pageSize = 100;
    private const int _maximumPages = 10;
    private const int _maximumTerms = 16;

    /// <summary>Gets the key the source is registered and selected under.</summary>
    internal static RetrievalSourceKey Key { get; } = new("agentkit.memory.durable");

    /// <inheritdoc/>
    public RetrievalSourceDescriptor Descriptor { get; } = new(Key, "1", requiresEmbedding: false, new ComponentId("agentkit.memory.durable-source"));

    /// <inheritdoc/>
    public async ValueTask<RetrievalSourceResult> SearchAsync(RetrievalSourceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Stores.MemoryStore is not { } store)
        {
            return RetrievalSourceResult.Failed(new RetrievalFailure(RetrievalFailureKind.SourcesUnavailable, "The profile names no durable-memory store."));
        }

        var terms = Terms(request.Query.Query.Text);
        if (terms.Length == 0)
        {
            return RetrievalSourceResult.Succeeded([], null);
        }

        ImmutableArray<MemoryNamespace?> namespaces = request.Query.Scope.Namespaces.IsEmpty
            ? [null]
            : [.. request.Query.Scope.Namespaces.Select(static scope => (MemoryNamespace?) scope)];
        var scored = new List<(DurableMemoryRecord Record, double Score)>();
        long? generation = null;
        var authorization = request.Query.Context.Authorization;
        var now = time.GetUtcNow();
        foreach (var scope in namespaces)
        {
            long after = 0;
            for (var page = 0; page < _maximumPages; page++)
            {
                var issue = await grants.IssueAsync(
                    authorities, authorization, store.Descriptor.SecurityAudience, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                    [MemorySecurityBinding.CollectionResource(authorization.Scope.AgentId)],
                    MemorySecurityBinding.ListFingerprint(scope, default, terms, after, _pageSize), cancellationToken).ConfigureAwait(false);
                if (issue.Grant is null)
                {
                    return RetrievalSourceResult.Failed(new RetrievalFailure(RetrievalFailureKind.Denied, issue.SafeMessage ?? "The memory read was not authorized."));
                }

                var listed = await store.ListAsync(new MemoryListRequest(scope, default, terms, after, _pageSize, issue.Grant), cancellationToken).ConfigureAwait(false);
                if (!listed.IsPage)
                {
                    return RetrievalSourceResult.Failed(new RetrievalFailure(RetrievalFailureKind.SourcesUnavailable, listed.Failure.SafeMessage));
                }

                generation = Math.Max(generation ?? 0, listed.DeletionGeneration);
                foreach (var record in listed.Items)
                {
                    if (record.Retention.ExpiresAt is { } expiry && expiry <= now)
                    {
                        continue;
                    }

                    var matched = terms.Count(term => record.Content.Text.Contains(term, StringComparison.OrdinalIgnoreCase));
                    scored.Add((record, (double) matched / terms.Length));
                }

                if (listed.NextCursor is not { } next)
                {
                    break;
                }

                after = next;
            }
        }

        var candidates = scored
            .OrderByDescending(static item => item.Score)
            .ThenBy(static item => item.Record.CreatedAt)
            .ThenBy(static item => item.Record.Id.Value)
            .Take(request.Limit)
            .Select(item => new RetrievalCandidate(
                request.Query.Id,
                new RetrievalSourceIdentity(Key, Descriptor.Version, generation),
                item.Record.Id,
                null,
                null,
                new CandidateContent(item.Record.Content.Text),
                item.Record.Provenance,
                TrustClassification.UntrustedData,
                item.Score,
                item.Record.Classification))
            .ToImmutableArray();
        return RetrievalSourceResult.Succeeded(candidates, generation);
    }

    private static ImmutableArray<string> Terms(string text)
    {
        var terms = new List<string>();
        var start = -1;
        for (var index = 0; index <= text.Length; index++)
        {
            var isWord = index < text.Length && char.IsLetterOrDigit(text[index]);
            if (isWord && start < 0)
            {
                start = index;
            }
            else if (!isWord && start >= 0)
            {
                if (index - start >= 2)
                {
                    terms.Add(text[start..index].ToLowerInvariant());
                }

                start = -1;
            }
        }

        return [.. terms.Distinct(StringComparer.Ordinal).Take(_maximumTerms)];
    }
}
