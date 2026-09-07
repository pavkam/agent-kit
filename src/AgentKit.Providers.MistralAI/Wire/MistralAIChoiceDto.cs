// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Wire;

/// <summary>
/// The wire shape of one entry in a <c>ChatCompletionResponse.choices</c>
/// array. Only the first choice is used; AgentKit's <see cref="ILlmModel"/>
/// contract does not model multiple concurrent response candidates.
/// </summary>
internal sealed class MistralAIChoiceDto
{
    /// <summary>Gets or sets the zero-based choice index.</summary>
    [JsonPropertyName("index")]
    public int? Index { get; set; }

    /// <summary>Gets or sets the committed assistant message for this choice.</summary>
    [JsonPropertyName("message")]
    public MistralAIAssistantMessageDto? Message { get; set; }

    /// <summary>Gets or sets the provider's raw, unnormalized stop reason text.</summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}
