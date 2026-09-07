// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Wire;

/// <summary>
/// The wire shape of a buffered <c>POST /model/{modelId}/converse</c>
/// response body.
/// </summary>
internal sealed class AwsBedrockConverseResponseDto
{
    /// <summary>Gets or sets the model output.</summary>
    [JsonPropertyName("output")]
    public AwsBedrockConverseOutputDto? Output { get; set; }

    /// <summary>Gets or sets the provider's raw, unnormalized stop reason.</summary>
    [JsonPropertyName("stopReason")]
    public string? StopReason { get; set; }

    /// <summary>Gets or sets token usage for the request.</summary>
    [JsonPropertyName("usage")]
    public AwsBedrockTokenUsageDto? Usage { get; set; }
}
