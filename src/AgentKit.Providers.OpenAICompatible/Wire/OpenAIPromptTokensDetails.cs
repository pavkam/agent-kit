// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// The wire shape of <c>usage.prompt_tokens_details</c> in an
/// OpenAI-compatible chat completion response.
/// </summary>
internal sealed class OpenAIPromptTokensDetails
{
    /// <summary>Gets or sets the number of prompt tokens served from a provider cache.</summary>
    [JsonPropertyName("cached_tokens")]
    public long? CachedTokens { get; set; }
}
