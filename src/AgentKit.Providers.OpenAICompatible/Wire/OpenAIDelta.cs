// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// The wire shape of one <c>choices[].delta</c> fragment in a streaming
/// OpenAI-compatible chat completion chunk.
/// </summary>
internal sealed class OpenAIDelta
{
    /// <summary>
    /// Gets or sets the message role, present only on the first chunk of a
    /// response.
    /// </summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>Gets or sets the incremental visible text fragment carried by this chunk.</summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>Gets or sets the incremental tool-call fragments carried by this chunk.</summary>
    [JsonPropertyName("tool_calls")]
    public IReadOnlyList<OpenAIToolCallDelta>? ToolCalls { get; set; }
}
