// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Wire;

/// <summary>
/// The wire shape of one entry in a streamed <c>CompletionChunk.choices</c>
/// array.
/// </summary>
internal sealed class MistralAIStreamChoiceDto
{
    /// <summary>Gets or sets the zero-based choice index.</summary>
    [JsonPropertyName("index")]
    public int? Index { get; set; }

    /// <summary>Gets or sets the incremental content and tool-call fragments for this chunk.</summary>
    [JsonPropertyName("delta")]
    public MistralAIDeltaMessageDto? Delta { get; set; }

    /// <summary>
    /// Gets or sets the provider's raw, unnormalized stop reason text, only
    /// present on the final chunk for this choice.
    /// </summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}
