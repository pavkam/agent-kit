// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Wire;

/// <summary>
/// The wire shape of the <c>output</c> union in a buffered
/// <c>Converse</c> response. Only the <c>message</c> member is translated.
/// </summary>
internal sealed class AwsBedrockConverseOutputDto
{
    /// <summary>Gets or sets the committed assistant message.</summary>
    [JsonPropertyName("message")]
    public AwsBedrockMessageDto? Message { get; set; }
}
