// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Wire;

/// <summary>
/// The wire shape of a Cohere v2 <c>POST /v2/embed</c> response body.
/// </summary>
/// <remarks>
/// Unlike every other provider covered by this repository, Cohere's
/// <c>embeddings</c> field is not an array of per-input results; it is an
/// object keyed by encoding name (<c>"float"</c>, <c>"int8"</c>,
/// <c>"uint8"</c>, <c>"binary"</c>, <c>"ubinary"</c>), because a single
/// Cohere request can ask for several encodings of the same input
/// simultaneously. This repository's portable contract requests exactly
/// one encoding per call, so the parser reads only the single key matching
/// the request's requested encoding. Neither the per-encoding array nor
/// the response as a whole carries an explicit per-item index; vectors are
/// positional and map onto the request's input order.
/// </remarks>
internal sealed class CohereEmbedResponseDto
{
    /// <summary>Gets or sets the response identifier Cohere assigned.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the encoding-keyed map of embedding vectors, each
    /// value a JSON array of one array of numbers per input, in input
    /// order.
    /// </summary>
    [JsonPropertyName("embeddings")]
    public Dictionary<string, JsonElement>? Embeddings { get; set; }

    /// <summary>Gets or sets usage and billing accounting for the request.</summary>
    [JsonPropertyName("meta")]
    public CohereEmbedMetaDto? Meta { get; set; }
}
