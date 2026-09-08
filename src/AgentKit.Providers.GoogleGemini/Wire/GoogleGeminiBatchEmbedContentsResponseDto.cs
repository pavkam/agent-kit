// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Wire;

/// <summary>
/// The wire shape of a Gemini <c>batchEmbedContents</c> response body.
/// </summary>
/// <remarks>
/// Unlike the OpenAI-compatible embeddings dialects, this response carries
/// no explicit per-item index: each entry in <see cref="Embeddings"/>
/// corresponds positionally to the same-position entry in the request's
/// <c>requests</c> array.
/// </remarks>
internal sealed class GoogleGeminiBatchEmbedContentsResponseDto
{
    /// <summary>Gets or sets the per-input embedding results, in request order.</summary>
    [JsonPropertyName("embeddings")]
    public IReadOnlyList<GoogleGeminiEmbeddingValuesDto>? Embeddings { get; set; }
}
