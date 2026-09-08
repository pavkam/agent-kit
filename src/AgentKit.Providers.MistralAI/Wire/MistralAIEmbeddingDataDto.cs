// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Wire;

/// <summary>
/// The wire shape of one entry in a Mistral AI embeddings response's
/// <c>data</c> array.
/// </summary>
internal sealed class MistralAIEmbeddingDataDto
{
    /// <summary>
    /// Gets or sets the embedding vector, a JSON array whose element
    /// interpretation depends on the request's <c>output_dtype</c>.
    /// </summary>
    [JsonPropertyName("embedding")]
    public JsonElement Embedding { get; set; }

    /// <summary>
    /// Gets or sets the zero-based position of this result within the
    /// request's <c>input</c> array. Always trust this over array order.
    /// </summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }
}
