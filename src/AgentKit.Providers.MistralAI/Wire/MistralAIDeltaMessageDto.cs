// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Wire;

/// <summary>
/// The wire shape of the <c>delta</c> object carried by a streamed
/// <c>CompletionResponseStreamChoice</c>.
/// </summary>
internal sealed class MistralAIDeltaMessageDto
{
    /// <summary>Gets or sets the message role, present only on the first chunk of a choice.</summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>
    /// Gets or sets the raw content node for this fragment, which may be a
    /// JSON string (a plain incremental text fragment) or a JSON array of
    /// content chunks, depending on whether the response contains
    /// reasoning content.
    /// </summary>
    [JsonPropertyName("content")]
    public JsonElement? Content { get; set; }

    /// <summary>Gets or sets the tool-call fragments carried by this delta, when any.</summary>
    [JsonPropertyName("tool_calls")]
    public IReadOnlyList<MistralAIToolCallDto>? ToolCalls { get; set; }
}
