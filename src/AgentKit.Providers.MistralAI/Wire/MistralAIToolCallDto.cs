// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Wire;

/// <summary>
/// The wire shape of one entry in an <c>AssistantMessage.tool_calls</c> or
/// <c>DeltaMessage.tool_calls</c> array.
/// </summary>
internal sealed class MistralAIToolCallDto
{
    /// <summary>Gets or sets the provider-supplied call identifier.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Gets or sets the tool type, always <c>"function"</c> for a client-executed call.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Gets or sets the requested function call.</summary>
    [JsonPropertyName("function")]
    public MistralAIToolCallFunctionDto? Function { get; set; }

    /// <summary>
    /// Gets or sets the ordinal position of this tool call, used to
    /// correlate streamed argument fragments across chunks for the same
    /// call.
    /// </summary>
    [JsonPropertyName("index")]
    public int? Index { get; set; }
}
