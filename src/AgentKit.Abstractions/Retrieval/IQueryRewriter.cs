// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Rewrites a retrieval query's text, recording the rewriter that did so.</summary>
/// <remarks>A rewriter changes only query content. It cannot change scope, budget, classification, identity, or authorization, and its output is validated by the pipeline. Implementations are thread-safe and never treat the query as instruction.</remarks>
public interface IQueryRewriter
{
    /// <summary>Gets the rewriter's key and version.</summary>
    /// <value>An immutable descriptor that does not change after construction.</value>
    public QueryRewriterDescriptor Descriptor { get; }

    /// <summary>Rewrites one query.</summary>
    /// <param name="query">The query whose content is rewritten.</param>
    /// <param name="cancellationToken">Cancels the rewrite.</param>
    /// <returns>The rewritten content carrying this rewriter's reference, or a content-safe rejection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<QueryRewriteResult> RewriteAsync(RetrievalQuery query, CancellationToken cancellationToken = default);
}
