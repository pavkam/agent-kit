// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI.Wire;

/// <summary>
/// The wire shape of a Vertex AI generic <c>:predict</c> response body, for
/// a text-embedding model.
/// </summary>
/// <remarks>
/// Like the Gemini Developer API's <c>batchEmbedContents</c> response,
/// this carries no explicit per-item index: each entry in
/// <see cref="Predictions"/> corresponds positionally to the same-position
/// entry in the request's <c>instances</c> array.
/// </remarks>
internal sealed class GoogleVertexAIPredictResponseDto
{
    /// <summary>Gets or sets the per-input predictions, in request order.</summary>
    [JsonPropertyName("predictions")]
    public IReadOnlyList<GoogleVertexAIEmbeddingPredictionDto>? Predictions { get; set; }
}
