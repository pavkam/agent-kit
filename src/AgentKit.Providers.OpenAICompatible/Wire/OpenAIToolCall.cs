// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// The wire shape of one committed <c>message.tool_calls[]</c> entry in a
/// non-streaming OpenAI-compatible chat completion response.
/// </summary>
internal sealed class OpenAIToolCall
{
    /// <summary>Gets or sets the provider-supplied call identifier.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>Gets or sets the call kind, always <c>"function"</c> for the current API.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>Gets or sets the requested function call.</summary>
    [JsonPropertyName("function")]
    public required OpenAIFunctionCall Function { get; set; }
}
