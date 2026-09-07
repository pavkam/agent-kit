// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Wire;

/// <summary>
/// The wire shape of the <c>ContentBlockStart</c> union carried by a
/// <c>contentBlockStart</c> event's <c>start</c> field. Only the
/// <c>toolUse</c> member is translated; a block with none of the union
/// members present is treated as a <c>text</c> block, which has no
/// discriminated start payload.
/// </summary>
internal sealed class AwsBedrockContentBlockStartDto
{
    /// <summary>Gets or sets the tool call being opened, present on a <c>toolUse</c> block.</summary>
    [JsonPropertyName("toolUse")]
    public AwsBedrockToolUseBlockStartDto? ToolUse { get; set; }
}
