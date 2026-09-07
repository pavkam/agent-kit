// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic.Wire;

/// <summary>
/// The wire shape of an Anthropic error envelope, whether returned as an
/// HTTP error body or as the <c>data</c> payload of an in-stream
/// <c>error</c> event.
/// </summary>
internal sealed class AnthropicErrorEnvelopeDto
{
    /// <summary>Gets or sets the envelope kind, normally <c>"error"</c>.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Gets or sets the nested error detail.</summary>
    [JsonPropertyName("error")]
    public AnthropicErrorDetailDto? Error { get; set; }

    /// <summary>Gets or sets the provider-supplied request correlation identifier, when available.</summary>
    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }
}
