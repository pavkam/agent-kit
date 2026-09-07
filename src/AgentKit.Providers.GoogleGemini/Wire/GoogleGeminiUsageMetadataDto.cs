// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Wire;

/// <summary>
/// The wire shape of the <c>usageMetadata</c> object in a
/// <c>GenerateContentResponse</c>.
/// </summary>
internal sealed class GoogleGeminiUsageMetadataDto
{
    /// <summary>Gets or sets the number of input (prompt) tokens consumed.</summary>
    [JsonPropertyName("promptTokenCount")]
    public long? PromptTokenCount { get; set; }

    /// <summary>Gets or sets the number of output (candidate) tokens produced.</summary>
    [JsonPropertyName("candidatesTokenCount")]
    public long? CandidatesTokenCount { get; set; }

    /// <summary>Gets or sets the total token count for the request.</summary>
    [JsonPropertyName("totalTokenCount")]
    public long? TotalTokenCount { get; set; }

    /// <summary>Gets or sets the number of prompt tokens served from cached content.</summary>
    [JsonPropertyName("cachedContentTokenCount")]
    public long? CachedContentTokenCount { get; set; }

    /// <summary>Gets or sets the number of hidden reasoning ("thoughts") tokens billed.</summary>
    [JsonPropertyName("thoughtsTokenCount")]
    public long? ThoughtsTokenCount { get; set; }

    /// <summary>Gets or sets the number of tokens consumed by tool-use prompts.</summary>
    [JsonPropertyName("toolUsePromptTokenCount")]
    public long? ToolUsePromptTokenCount { get; set; }
}
