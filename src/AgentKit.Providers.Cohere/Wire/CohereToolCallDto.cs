// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Wire;

/// <summary>
/// The wire shape of one tool call. This shape is reused for a buffered
/// response's <c>message.tool_calls[]</c> entries and for the singular
/// <c>delta.message.tool_calls</c> object carried by a streaming
/// <c>tool-call-start</c> or <c>tool-call-delta</c> event.
/// </summary>
internal sealed class CohereToolCallDto
{
    /// <summary>Gets or sets the provider-supplied call identifier.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Gets or sets the tool type, always <c>"function"</c> for a client-executed call.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Gets or sets the requested function call.</summary>
    [JsonPropertyName("function")]
    public CohereToolCallFunctionDto? Function { get; set; }
}
