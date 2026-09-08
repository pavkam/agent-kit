// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Wire;

/// <summary>
/// The wire shape of a Mistral AI <c>POST /v1/embeddings</c> response body.
/// </summary>
internal sealed class MistralAIEmbeddingResponseDto
{
    /// <summary>Gets or sets the per-input embedding results.</summary>
    [JsonPropertyName("data")]
    public IReadOnlyList<MistralAIEmbeddingDataDto>? Data { get; set; }

    /// <summary>Gets or sets the model that actually served the request.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>Gets or sets token usage for the request.</summary>
    [JsonPropertyName("usage")]
    public MistralAIUsageDto? Usage { get; set; }
}
