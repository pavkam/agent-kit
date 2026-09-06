// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// The wire shape of one committed <c>message.tool_calls[].function</c>
/// object in a non-streaming OpenAI-compatible chat completion response.
/// </summary>
internal sealed class OpenAIFunctionCall
{
    /// <summary>Gets or sets the tool/function name the model chose to call.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the raw, unparsed JSON text of the call arguments
    /// exactly as returned by the provider.
    /// </summary>
    [JsonPropertyName("arguments")]
    public required string Arguments { get; set; }
}
