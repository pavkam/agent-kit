// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// The wire shape of one incremental <c>delta.tool_calls[].function</c>
/// fragment in a streaming OpenAI-compatible chat completion chunk.
/// </summary>
internal sealed class OpenAIFunctionCallDelta
{
    /// <summary>
    /// Gets or sets the tool/function name, present only on the first
    /// chunk that introduces a given tool call.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the incremental raw JSON text fragment of the call
    /// arguments carried by this chunk.
    /// </summary>
    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }
}
