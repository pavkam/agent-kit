// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic.Wire;

/// <summary>
/// The wire shape of one Anthropic Messages server-sent event payload,
/// covering the union of fields used by <c>message_start</c>,
/// <c>content_block_start</c>, <c>content_block_delta</c>,
/// <c>content_block_stop</c>, <c>message_delta</c>, <c>message_stop</c>,
/// <c>ping</c>, and <c>error</c> events.
/// </summary>
internal sealed class AnthropicStreamEventDto
{
    /// <summary>Gets or sets the event kind discriminator.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>
    /// Gets or sets the content-block index this event applies to, present
    /// on <c>content_block_start</c>, <c>content_block_delta</c>, and
    /// <c>content_block_stop</c>.
    /// </summary>
    [JsonPropertyName("index")]
    public int? Index { get; set; }

    /// <summary>Gets or sets the partial message envelope, present on <c>message_start</c>.</summary>
    [JsonPropertyName("message")]
    public AnthropicMessageResponseDto? Message { get; set; }

    /// <summary>Gets or sets the newly opened content block, present on <c>content_block_start</c>.</summary>
    [JsonPropertyName("content_block")]
    public AnthropicContentBlockDto? ContentBlock { get; set; }

    /// <summary>
    /// Gets or sets the incremental delta, present on
    /// <c>content_block_delta</c> and <c>message_delta</c>.
    /// </summary>
    [JsonPropertyName("delta")]
    public AnthropicStreamDeltaDto? Delta { get; set; }

    /// <summary>Gets or sets cumulative usage, present on <c>message_delta</c>.</summary>
    [JsonPropertyName("usage")]
    public AnthropicUsageDto? Usage { get; set; }

    /// <summary>Gets or sets the error detail, present on an <c>error</c> event.</summary>
    [JsonPropertyName("error")]
    public AnthropicErrorDetailDto? Error { get; set; }
}
