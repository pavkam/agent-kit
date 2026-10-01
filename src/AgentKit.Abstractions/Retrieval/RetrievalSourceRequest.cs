// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks one retrieval source to search for candidates.</summary>
/// <remarks>
/// The <see cref="Query"/> carries the effective (possibly rewritten) query, already authorized and narrowed by the
/// pipeline. The <see cref="Grant"/> is the bounded authorization the pipeline obtained for this source's read; a source that
/// reads a store obtains that store's own grants. A source returns data with provenance and never instructions.
/// </remarks>
public sealed record RetrievalSourceRequest
{
    /// <summary>Initializes a validated request.</summary>
    /// <param name="query">The effective query.</param>
    /// <param name="embedding">The query embedding, or <see langword="null"/> when the source does not need one or none was available.</param>
    /// <param name="limit">The positive largest number of candidates the source should return.</param>
    /// <param name="grant">The bounded grant for this source's read.</param>
    /// <param name="stores">The stores the profile captured for this operation.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is not positive.</exception>
    /// <exception cref="ArgumentException">The grant lacks captured authorization.</exception>
    public RetrievalSourceRequest(RetrievalQuery query, RetrievalQueryEmbedding? embedding, int limit, SecurityGrant grant, RetrievalSourceStores stores)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant.Authorization, nameof(grant));
        ArgumentNullException.ThrowIfNull(stores);
        Query = query;
        Embedding = embedding;
        Limit = limit;
        Grant = grant;
        Stores = stores;
    }

    /// <summary>Gets the effective query.</summary>
    public RetrievalQuery Query { get; }

    /// <summary>Gets the query embedding, or <see langword="null"/>.</summary>
    public RetrievalQueryEmbedding? Embedding { get; }

    /// <summary>Gets the largest number of candidates the source should return.</summary>
    public int Limit { get; }

    /// <summary>Gets the bounded grant for this source's read.</summary>
    public SecurityGrant Grant { get; }

    /// <summary>Gets the stores the profile captured for this operation.</summary>
    public RetrievalSourceStores Stores { get; }
}
