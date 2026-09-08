// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI.Wire;

/// <summary>
/// The wire shape of one entry in a Vertex AI <c>:predict</c> response's
/// <c>predictions</c> array, for a text-embedding model.
/// </summary>
internal sealed class GoogleVertexAIEmbeddingPredictionDto
{
    /// <summary>Gets or sets the computed embedding and its accounting.</summary>
    [JsonPropertyName("embeddings")]
    public GoogleVertexAIEmbeddingValuesDto? Embeddings { get; set; }
}
