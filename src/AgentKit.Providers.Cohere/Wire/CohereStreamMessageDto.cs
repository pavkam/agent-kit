// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Wire;

/// <summary>
/// The wire shape of the <c>delta.message</c> object carried by a
/// streaming event. Unlike the buffered <see cref="CohereAssistantMessageDto"/>,
/// <see cref="Content"/> and <see cref="ToolCalls"/> are each a single
/// object here, not an array, because each streaming event pertains to
/// exactly one content block or tool call, correlated by the event's own
/// <c>index</c> field.
/// </summary>
internal sealed class CohereStreamMessageDto
{
    /// <summary>Gets or sets the message role, present only on the <c>message-start</c> event.</summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>Gets or sets the single content-block fragment this event carries, when any.</summary>
    [JsonPropertyName("content")]
    public CohereContentBlockDto? Content { get; set; }

    /// <summary>Gets or sets the single tool-call fragment this event carries, when any.</summary>
    [JsonPropertyName("tool_calls")]
    public CohereToolCallDto? ToolCalls { get; set; }

    /// <summary>
    /// Gets or sets the tool-use plan text fragment this event carries,
    /// not yet round-tripped by this package.
    /// </summary>
    [JsonPropertyName("tool_plan")]
    public string? ToolPlan { get; set; }
}
