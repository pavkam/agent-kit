// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Wire;

/// <summary>
/// The wire shape of one <c>ContentEmbedding</c> entry within a
/// <c>batchEmbedContents</c> response's <c>embeddings</c> array.
/// </summary>
internal sealed class GoogleGeminiEmbeddingValuesDto
{
    /// <summary>Gets or sets the ordered floating-point vector elements.</summary>
    [JsonPropertyName("values")]
    public IReadOnlyList<float>? Values { get; set; }
}
