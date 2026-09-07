// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The portable intended use of an embedding, mapped onto each provider's
/// own task-type/input-type vocabulary (for example Cohere's
/// <c>search_query</c>/<c>search_document</c> or Google's
/// <c>RETRIEVAL_QUERY</c>/<c>RETRIEVAL_DOCUMENT</c>).
/// </summary>
/// <remarks>
/// A provider that applies an asymmetric transform for query versus
/// document embeddings produces vectors from a different effective
/// embedding space for each purpose; two vectors are comparable only when
/// their purposes are compatible. See <see cref="EmbeddingSpaceIdentity"/>.
/// </remarks>
public enum EmbeddingPurpose
{
    /// <summary>No specific purpose was requested; the provider's default behavior applies.</summary>
    Unspecified,

    /// <summary>The embedding represents a search query, for retrieval against document embeddings.</summary>
    Query,

    /// <summary>The embedding represents a document to be retrieved by query embeddings.</summary>
    Document,

    /// <summary>The embedding is intended for symmetric semantic-similarity comparison.</summary>
    Similarity,

    /// <summary>The embedding is intended as input to a downstream classification model.</summary>
    Classification,

    /// <summary>The embedding is intended as input to a downstream clustering algorithm.</summary>
    Clustering,

    /// <summary>The embedding represents a question for a question-answering retrieval task.</summary>
    QuestionAnswering,

    /// <summary>The embedding represents a query for a code-retrieval task.</summary>
    CodeRetrieval,
}
