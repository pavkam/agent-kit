// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// The wire shape of the <c>usage</c> object in an OpenAI-compatible chat
/// completion response or final streaming chunk.
/// </summary>
internal sealed class OpenAIUsage
{
    /// <summary>Gets or sets the number of prompt (input) tokens consumed.</summary>
    [JsonPropertyName("prompt_tokens")]
    public long? PromptTokens { get; set; }

    /// <summary>Gets or sets the number of completion (output) tokens produced.</summary>
    [JsonPropertyName("completion_tokens")]
    public long? CompletionTokens { get; set; }

    /// <summary>Gets or sets the total token count for the request.</summary>
    [JsonPropertyName("total_tokens")]
    public long? TotalTokens { get; set; }

    /// <summary>Gets or sets prompt-token cache accounting detail, when reported.</summary>
    [JsonPropertyName("prompt_tokens_details")]
    public OpenAIPromptTokensDetails? PromptTokensDetails { get; set; }

    /// <summary>Gets or sets completion-token reasoning accounting detail, when reported.</summary>
    [JsonPropertyName("completion_tokens_details")]
    public OpenAICompletionTokensDetails? CompletionTokensDetails { get; set; }
}
