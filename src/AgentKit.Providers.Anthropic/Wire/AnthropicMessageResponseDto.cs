// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic.Wire;

/// <summary>
/// The wire shape of a complete, non-streaming Anthropic Messages response
/// body, and of the nested <c>message</c> object carried by a streaming
/// <c>message_start</c> event (whose <c>content</c> is always empty and
/// whose <c>usage</c> reports only input tokens at that point).
/// </summary>
internal sealed class AnthropicMessageResponseDto
{
    /// <summary>Gets or sets the provider-supplied message identifier.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Gets or sets the resolved model identifier that actually served the request.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>Gets or sets the ordered response content blocks.</summary>
    [JsonPropertyName("content")]
    public IReadOnlyList<AnthropicContentBlockDto>? Content { get; set; }

    /// <summary>Gets or sets the provider's raw, unnormalized stop reason text.</summary>
    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }

    /// <summary>Gets or sets the custom stop sequence that triggered termination, when applicable.</summary>
    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; set; }

    /// <summary>Gets or sets token usage for the response, when reported at this point.</summary>
    [JsonPropertyName("usage")]
    public AnthropicUsageDto? Usage { get; set; }
}
