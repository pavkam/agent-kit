// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Wire;

/// <summary>
/// The wire shape of the <c>usage</c> object in a
/// <c>ChatCompletionResponse</c> or streamed <c>CompletionChunk</c>.
/// </summary>
internal sealed class MistralAIUsageDto
{
    /// <summary>Gets or sets the number of input (prompt) tokens consumed.</summary>
    [JsonPropertyName("prompt_tokens")]
    public long? PromptTokens { get; set; }

    /// <summary>Gets or sets the number of output (completion) tokens produced.</summary>
    [JsonPropertyName("completion_tokens")]
    public long? CompletionTokens { get; set; }

    /// <summary>Gets or sets the total token count for the request.</summary>
    [JsonPropertyName("total_tokens")]
    public long? TotalTokens { get; set; }
}
