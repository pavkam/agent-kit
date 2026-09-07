// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Wire;

/// <summary>
/// The wire shape of the <c>message</c> object carried by a buffered
/// <c>ChatCompletionChoice</c>.
/// </summary>
internal sealed class MistralAIAssistantMessageDto
{
    /// <summary>Gets or sets the message role, normally <c>"assistant"</c>.</summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>
    /// Gets or sets the raw content node, which may be a JSON string, a
    /// JSON array of content chunks, or JSON <c>null</c>, depending on
    /// whether the response contains reasoning content.
    /// </summary>
    [JsonPropertyName("content")]
    public JsonElement? Content { get; set; }

    /// <summary>Gets or sets the tool calls the model requested, when any.</summary>
    [JsonPropertyName("tool_calls")]
    public IReadOnlyList<MistralAIToolCallDto>? ToolCalls { get; set; }
}
