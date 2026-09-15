// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// The wire shape of one <c>choices[].message</c> object in a non-streaming
/// OpenAI-compatible chat completion response.
/// </summary>
internal sealed class OpenAIResponseMessage
{
    /// <summary>Gets or sets the message role, normally <c>"assistant"</c>.</summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>Gets or sets the visible response text, when the model produced any.</summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>Gets or sets the tool calls the model requested, when any.</summary>
    [JsonPropertyName("tool_calls")]
    public IReadOnlyList<OpenAIToolCall>? ToolCalls { get; set; }

    /// <summary>Gets or sets visible reasoning some compatible deployments return under <c>reasoning_content</c>.</summary>
    [JsonPropertyName("reasoning_content")]
    public string? ReasoningContent { get; set; }

    /// <summary>Gets or sets visible reasoning returned under the alternative <c>reasoning</c> member.</summary>
    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; set; }
}
