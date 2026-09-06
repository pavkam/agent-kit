// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// The wire shape of one incremental <c>delta.tool_calls[]</c> fragment in a
/// streaming OpenAI-compatible chat completion chunk.
/// </summary>
internal sealed class OpenAIToolCallDelta
{
    /// <summary>
    /// Gets or sets the zero-based slot index of the tool call this
    /// fragment belongs to, stable across every chunk that contributes to
    /// the same call, which distinguishes parallel tool-call streams.
    /// </summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the provider-supplied call identifier, present only on
    /// the first chunk that introduces this call.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the call kind, present only on the first chunk that
    /// introduces this call.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Gets or sets the incremental function-call fragment.</summary>
    [JsonPropertyName("function")]
    public OpenAIFunctionCallDelta? Function { get; set; }
}
