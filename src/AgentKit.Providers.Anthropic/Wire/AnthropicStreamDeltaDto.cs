// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic.Wire;

/// <summary>
/// The wire shape of the nested <c>delta</c> object carried by a
/// <c>content_block_delta</c> or <c>message_delta</c> streaming event,
/// covering the union of fields used by <c>text_delta</c>,
/// <c>input_json_delta</c>, <c>thinking_delta</c>, <c>signature_delta</c>,
/// and the message-level stop-reason delta.
/// </summary>
internal sealed class AnthropicStreamDeltaDto
{
    /// <summary>Gets or sets the delta kind discriminator, present on content-block deltas.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Gets or sets the incremental visible text fragment, present on <c>text_delta</c>.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>Gets or sets the incremental raw JSON fragment of a tool call input, present on <c>input_json_delta</c>.</summary>
    [JsonPropertyName("partial_json")]
    public string? PartialJson { get; set; }

    /// <summary>Gets or sets the incremental reasoning text fragment, present on <c>thinking_delta</c>.</summary>
    [JsonPropertyName("thinking")]
    public string? Thinking { get; set; }

    /// <summary>Gets or sets the opaque provider signature fragment, present on <c>signature_delta</c>.</summary>
    [JsonPropertyName("signature")]
    public string? Signature { get; set; }

    /// <summary>Gets or sets the provider's raw, unnormalized stop reason text, present on a <c>message_delta</c> event.</summary>
    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }

    /// <summary>Gets or sets the custom stop sequence that triggered termination, present on a <c>message_delta</c> event.</summary>
    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; set; }

    /// <summary>
    /// Gets or sets any fields not modeled above, captured so a future or
    /// unrecognized delta kind (for example, a citation delta) can be
    /// preserved as a <see cref="ProviderContentDelta"/> instead of
    /// silently dropped.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
