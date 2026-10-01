// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Is the outcome of rewriting a query: the new query content with provenance, or a content-safe rejection.</summary>
public sealed record QueryRewriteResult
{
    private QueryRewriteResult(RetrievalQueryContent? content, string? safeMessage)
    {
        Content = content;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the rewritten query content, or <see langword="null"/> when the rewrite was rejected.</summary>
    public RetrievalQueryContent? Content { get; }

    /// <summary>Gets the content-safe rejection reason, or <see langword="null"/> when the query was rewritten.</summary>
    public string? SafeMessage { get; }

    /// <summary>Gets a value indicating whether the query was rewritten.</summary>
    [MemberNotNullWhen(true, nameof(Content))]
    [MemberNotNullWhen(false, nameof(SafeMessage))]
    public bool IsRewritten => Content is not null;

    /// <summary>Creates a successful result.</summary>
    /// <param name="content">The rewritten content; it must record the rewriter that produced it.</param>
    /// <returns>A rewritten result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="content"/> does not record a rewriter.</exception>
    public static QueryRewriteResult Rewritten(RetrievalQueryContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNotEqual(content.IsRewritten, true, nameof(content));
        return new(content, null);
    }

    /// <summary>Creates a rejected result.</summary>
    /// <param name="safeMessage">The content-safe reason.</param>
    /// <returns>A rejected result.</returns>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is null or blank.</exception>
    public static QueryRewriteResult Rejected(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        return new(null, safeMessage);
    }
}
