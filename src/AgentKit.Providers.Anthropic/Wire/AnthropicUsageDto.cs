// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic.Wire;

/// <summary>
/// The wire shape of the <c>usage</c> object in an Anthropic Messages
/// response, <c>message_start</c> event, or <c>message_delta</c> event.
/// </summary>
internal sealed class AnthropicUsageDto
{
    /// <summary>Gets or sets the number of input tokens consumed.</summary>
    [JsonPropertyName("input_tokens")]
    public long? InputTokens { get; set; }

    /// <summary>Gets or sets the number of output tokens produced.</summary>
    [JsonPropertyName("output_tokens")]
    public long? OutputTokens { get; set; }

    /// <summary>Gets or sets the number of input tokens used to create a new prompt cache entry.</summary>
    [JsonPropertyName("cache_creation_input_tokens")]
    public long? CacheCreationInputTokens { get; set; }

    /// <summary>Gets or sets the number of input tokens served from the prompt cache.</summary>
    [JsonPropertyName("cache_read_input_tokens")]
    public long? CacheReadInputTokens { get; set; }
}
