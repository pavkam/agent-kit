// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Wire;

/// <summary>
/// The wire shape of a buffered <c>POST /v2/chat</c> response body.
/// </summary>
internal sealed class CohereChatResponseDto
{
    /// <summary>Gets or sets the provider-supplied reply identifier.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Gets or sets the provider's raw, unnormalized finish reason.</summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }

    /// <summary>Gets or sets the committed assistant message.</summary>
    [JsonPropertyName("message")]
    public CohereAssistantMessageDto? Message { get; set; }

    /// <summary>Gets or sets token usage for the request.</summary>
    [JsonPropertyName("usage")]
    public CohereUsageDto? Usage { get; set; }
}
