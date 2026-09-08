// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI.Wire;

/// <summary>
/// The wire shape of the <c>statistics</c> object within one Vertex AI
/// text-embedding prediction.
/// </summary>
internal sealed class GoogleVertexAIEmbeddingStatisticsDto
{
    /// <summary>Gets or sets whether the input was silently truncated to fit the model's token limit.</summary>
    [JsonPropertyName("truncated")]
    public bool Truncated { get; set; }

    /// <summary>Gets or sets the number of tokens the input was measured at.</summary>
    [JsonPropertyName("token_count")]
    public long? TokenCount { get; set; }
}
