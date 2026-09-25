// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One reranked document score within a response.</summary>
public sealed record RerankResult
{
    /// <summary>Initializes one rerank result.</summary>
    /// <param name="inputIndex">The source document index.</param>
    /// <param name="documentId">The caller correlation identity.</param>
    /// <param name="relevanceScore">The provider relevance score.</param>
    /// <param name="extensions">Provider-specific result data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="inputIndex"/> is negative.</exception>
    public RerankResult(int inputIndex, DocumentId documentId, double relevanceScore, ExtensionData extensions)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputIndex);
        ArgumentNullException.ThrowIfNull(extensions);
        InputIndex = inputIndex;
        DocumentId = documentId;
        RelevanceScore = relevanceScore;
        Extensions = extensions;
    }

    /// <summary>Gets the source document index.</summary>
    public int InputIndex { get; init; }

    /// <summary>Gets the caller correlation identity.</summary>
    public DocumentId DocumentId { get; init; }

    /// <summary>Gets the provider relevance score.</summary>
    public double RelevanceScore { get; init; }

    /// <summary>Gets provider-specific result data.</summary>
    public ExtensionData Extensions { get; init; }
}
