// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Searches one body of knowledge and returns candidates as data with provenance.</summary>
/// <remarks>
/// A source discovers and searches; it does not authorize exposure, rewrite the query, rerank across sources, deduplicate,
/// or apply the final budget. Implementations are thread-safe, stateless between calls, and treat everything they return as
/// untrusted data. A source must not retain the stores it is handed.
/// </remarks>
public interface IRetrievalSource
{
    /// <summary>Gets the source's identity, version, and needs.</summary>
    /// <value>An immutable descriptor that does not change after construction.</value>
    public RetrievalSourceDescriptor Descriptor { get; }

    /// <summary>Searches for candidates.</summary>
    /// <param name="request">The authorized request.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>Candidates with the observed watermarks, or a typed failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<RetrievalSourceResult> SearchAsync(RetrievalSourceRequest request, CancellationToken cancellationToken = default);
}
