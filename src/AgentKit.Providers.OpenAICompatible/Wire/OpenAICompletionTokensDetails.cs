// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// The wire shape of <c>usage.completion_tokens_details</c> in an
/// OpenAI-compatible chat completion response.
/// </summary>
internal sealed class OpenAICompletionTokensDetails
{
    /// <summary>Gets or sets the number of hidden reasoning tokens billed for the completion.</summary>
    [JsonPropertyName("reasoning_tokens")]
    public long? ReasoningTokens { get; set; }
}
