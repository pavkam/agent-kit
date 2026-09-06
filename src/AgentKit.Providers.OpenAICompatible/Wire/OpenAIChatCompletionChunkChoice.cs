// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// The wire shape of one <c>choices[]</c> entry in a streaming
/// OpenAI-compatible chat completion chunk.
/// </summary>
internal sealed class OpenAIChatCompletionChunkChoice
{
    /// <summary>Gets or sets the zero-based choice index.</summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }

    /// <summary>Gets or sets the incremental delta fragment for this choice.</summary>
    [JsonPropertyName("delta")]
    public OpenAIDelta? Delta { get; set; }

    /// <summary>
    /// Gets or sets the provider's raw, unnormalized stop reason text,
    /// present only on the chunk that closes this choice.
    /// </summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}
