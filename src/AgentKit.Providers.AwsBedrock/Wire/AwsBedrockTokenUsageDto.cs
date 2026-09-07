// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Wire;

/// <summary>
/// The wire shape of the <c>usage</c> object (<c>TokenUsage</c>) in a
/// buffered <c>Converse</c> response or the streaming <c>metadata</c>
/// event.
/// </summary>
internal sealed class AwsBedrockTokenUsageDto
{
    /// <summary>Gets or sets the number of input tokens.</summary>
    [JsonPropertyName("inputTokens")]
    public long? InputTokens { get; set; }

    /// <summary>Gets or sets the number of output tokens.</summary>
    [JsonPropertyName("outputTokens")]
    public long? OutputTokens { get; set; }

    /// <summary>Gets or sets the total token count.</summary>
    [JsonPropertyName("totalTokens")]
    public long? TotalTokens { get; set; }

    /// <summary>Gets or sets the number of input tokens read from the prompt cache.</summary>
    [JsonPropertyName("cacheReadInputTokens")]
    public long? CacheReadInputTokens { get; set; }

    /// <summary>Gets or sets the number of input tokens written to the prompt cache.</summary>
    [JsonPropertyName("cacheWriteInputTokens")]
    public long? CacheWriteInputTokens { get; set; }
}
