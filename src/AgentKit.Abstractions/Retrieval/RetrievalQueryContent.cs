// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries the text a retrieval searches for, recording whether a query rewriter changed it and who did.</summary>
/// <remarks>A rewriter changes only the query text, and every change keeps the original text and the exact rewriter key and version, so provenance survives rewriting. Query text is data and never becomes instruction.</remarks>
public sealed record RetrievalQueryContent
{
    /// <summary>The largest number of UTF-16 code units accepted in a query.</summary>
    public const int MaximumTextLength = 8_192;

    /// <summary>Initializes an original, unrewritten query.</summary>
    /// <param name="text">The non-blank query text, at most <see cref="MaximumTextLength"/> code units.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="text"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="text"/> exceeds <see cref="MaximumTextLength"/>.</exception>
    public RetrievalQueryContent(string text)
        : this(text, text, null)
    {
    }

    private RetrievalQueryContent(string text, string originalText, QueryRewriterReference? rewrittenBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, MaximumTextLength, nameof(text));
        Text = text;
        OriginalText = originalText;
        RewrittenBy = rewrittenBy;
    }

    /// <summary>Gets the text the retrieval searches for.</summary>
    public string Text { get; }

    /// <summary>Gets the query text before any rewriting.</summary>
    public string OriginalText { get; }

    /// <summary>Gets the rewriter that produced <see cref="Text"/>, or <see langword="null"/> for an original query.</summary>
    public QueryRewriterReference? RewrittenBy { get; }

    /// <summary>Gets a value indicating whether a rewriter produced <see cref="Text"/>.</summary>
    public bool IsRewritten => RewrittenBy is not null;

    /// <summary>Creates the rewritten form of this query, keeping the original text.</summary>
    /// <param name="text">The rewritten non-blank text.</param>
    /// <param name="rewriter">The exact rewriter key and version that produced it.</param>
    /// <returns>New content whose <see cref="OriginalText"/> is this query's original text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> or <paramref name="rewriter"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="text"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="text"/> exceeds <see cref="MaximumTextLength"/>.</exception>
    public RetrievalQueryContent Rewrite(string text, QueryRewriterReference rewriter)
    {
        ArgumentNullException.ThrowIfNull(rewriter);
        return new(text, OriginalText, rewriter);
    }
}
