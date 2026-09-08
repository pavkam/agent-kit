// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI.Wire;

/// <summary>
/// The wire shape of the <c>embeddings</c> object within one Vertex AI
/// text-embedding prediction.
/// </summary>
internal sealed class GoogleVertexAIEmbeddingValuesDto
{
    /// <summary>Gets or sets per-input truncation and token accounting.</summary>
    [JsonPropertyName("statistics")]
    public GoogleVertexAIEmbeddingStatisticsDto? Statistics { get; set; }

    /// <summary>Gets or sets the ordered floating-point vector elements.</summary>
    [JsonPropertyName("values")]
    public IReadOnlyList<float>? Values { get; set; }
}
