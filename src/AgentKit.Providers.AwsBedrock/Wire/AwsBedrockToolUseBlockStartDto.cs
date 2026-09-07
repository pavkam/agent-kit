// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Wire;

/// <summary>
/// The wire shape of a streaming <c>ToolUseBlockStart</c>, carried by a
/// <c>contentBlockStart</c> event's <c>start.toolUse</c> field.
/// </summary>
internal sealed class AwsBedrockToolUseBlockStartDto
{
    /// <summary>Gets or sets the provider-supplied tool call identifier.</summary>
    [JsonPropertyName("toolUseId")]
    public string? ToolUseId { get; set; }

    /// <summary>Gets or sets the tool/function name the model chose to call.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
