// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// The wire shape of one <c>choices[]</c> entry in a non-streaming
/// OpenAI-compatible chat completion response.
/// </summary>
internal sealed class OpenAIChatCompletionChoice
{
    /// <summary>Gets or sets the zero-based choice index.</summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }

    /// <summary>Gets or sets the committed message for this choice.</summary>
    [JsonPropertyName("message")]
    public required OpenAIResponseMessage Message { get; set; }

    /// <summary>Gets or sets the provider's raw, unnormalized stop reason text.</summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}
