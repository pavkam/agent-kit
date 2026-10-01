// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Returns the query text unchanged while recording this rewriter's key and version as the query's provenance.</summary>
/// <remarks>The rewriter is stateless and thread-safe. It is the package default, so enabling rewriting never requires inventing a model or an endpoint.</remarks>
internal sealed class NoRewriteQueryRewriter: IQueryRewriter
{
    /// <summary>Gets the key the rewriter is registered under.</summary>
    internal static QueryRewriterKey Key { get; } = new("agentkit.no-rewrite");

    /// <summary>Gets the version of the rewriter's behavior.</summary>
    internal static QueryRewriterVersion Version { get; } = new("1");

    /// <summary>Gets the descriptor the package registers the rewriter under.</summary>
    internal static QueryRewriterDescriptor DescriptorValue { get; } = new(Key, Version);

    /// <inheritdoc/>
    public QueryRewriterDescriptor Descriptor => DescriptorValue;

    /// <inheritdoc/>
    public ValueTask<QueryRewriteResult> RewriteAsync(RetrievalQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(QueryRewriteResult.Rewritten(query.Query.Rewrite(query.Query.Text, new QueryRewriterReference(Key, Version))));
    }
}
