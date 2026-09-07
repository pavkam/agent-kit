// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// The wire shape of one entry in an OpenAI-compatible embeddings
/// response's <c>data</c> array.
/// </summary>
internal sealed class OpenAIEmbeddingDataDto
{
    /// <summary>
    /// Gets or sets the embedding vector: a JSON array of numbers when
    /// <c>encoding_format</c> was <c>"float"</c>, or a base64-encoded
    /// string when it was <c>"base64"</c>.
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
