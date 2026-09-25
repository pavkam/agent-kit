// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The provider-neutral rerank request body.</summary>
public sealed record RerankRequest
{
    /// <summary>Initializes a rerank request.</summary>
    /// <param name="query">The query text.</param>
    /// <param name="documents">The ordered documents to score.</param>
    /// <param name="topCount">The maximum number of results to return, when supported.</param>
    /// <param name="options">Bounded provider options.</param>
    /// <exception cref="ArgumentException">The query or documents are invalid.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public RerankRequest(
        string query,
        ImmutableArray<RerankDocument> documents,
        int? topCount,
        ProviderRequestOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentException.ThrowIfDefaultOrEmpty(documents);
        ArgumentException.ThrowIfContainsNull(documents);
        if (topCount is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(topCount), topCount, "Value must be at least one.");
        }

        ArgumentNullException.ThrowIfNull(options);
        Query = query;
        Documents = documents;
        TopCount = topCount;
        Options = options;
    }

    /// <summary>Gets the query text.</summary>
    public string Query { get; init; }

    /// <summary>Gets the ordered documents.</summary>
    public ImmutableArray<RerankDocument> Documents { get; init; }

    /// <summary>Gets the maximum number of results to return, when supported.</summary>
    public int? TopCount { get; init; }

    /// <summary>Gets bounded provider options.</summary>
    public ProviderRequestOptions Options { get; init; }
}
