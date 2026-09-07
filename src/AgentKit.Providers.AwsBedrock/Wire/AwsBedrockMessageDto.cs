// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Wire;

/// <summary>
/// The wire shape of the <c>message</c> object in a Converse
/// <c>ConverseOutput</c>.
/// </summary>
internal sealed class AwsBedrockMessageDto
{
    /// <summary>Gets or sets the message role, normally <c>"assistant"</c>.</summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>Gets or sets the ordered content blocks.</summary>
    [JsonPropertyName("content")]
    public IReadOnlyList<AwsBedrockContentBlockDto>? Content { get; set; }
}
