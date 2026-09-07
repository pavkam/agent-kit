// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Wire;

/// <summary>
/// The wire shape of the <c>message</c> object in a buffered
/// <c>ChatResponse</c>.
/// </summary>
internal sealed class CohereAssistantMessageDto
{
    /// <summary>Gets or sets the message role, normally <c>"assistant"</c>.</summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>Gets or sets the ordered content blocks, when the response includes visible or reasoning content.</summary>
    [JsonPropertyName("content")]
    public IReadOnlyList<CohereContentBlockDto>? Content { get; set; }

    /// <summary>Gets or sets the tool calls the model requested, when any.</summary>
    [JsonPropertyName("tool_calls")]
    public IReadOnlyList<CohereToolCallDto>? ToolCalls { get; set; }

    /// <summary>
    /// Gets or sets the model's chain-of-thought style tool-use plan, not
    /// yet round-tripped by this package.
    /// </summary>
    [JsonPropertyName("tool_plan")]
    public string? ToolPlan { get; set; }
}
