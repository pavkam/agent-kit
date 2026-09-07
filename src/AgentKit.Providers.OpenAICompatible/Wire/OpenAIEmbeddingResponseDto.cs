// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// The wire shape of an OpenAI-compatible <c>POST /embeddings</c> response
/// body.
/// </summary>
internal sealed class OpenAIEmbeddingResponseDto
{
    /// <summary>Gets or sets the per-input embedding results.</summary>
    [JsonPropertyName("data")]
    public IReadOnlyList<OpenAIEmbeddingDataDto>? Data { get; set; }

    /// <summary>Gets or sets the model that actually served the request.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>Gets or sets token usage for the request.</summary>
    [JsonPropertyName("usage")]
    public OpenAIUsage? Usage { get; set; }
}
