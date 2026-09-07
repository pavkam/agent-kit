// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Wire;

/// <summary>
/// The wire shape of the <c>ContentBlockDelta</c> union carried by a
/// <c>contentBlockDelta</c> event's <c>delta</c> field. Only the
/// <c>text</c> and <c>toolUse</c> members are translated; <c>reasoningContent</c>,
/// <c>citation</c>, and <c>image</c> deltas are out of scope for this pass.
/// </summary>
internal sealed class AwsBedrockContentBlockDeltaDto
{
    /// <summary>Gets or sets the next fragment of visible text, present on a <c>text</c> delta.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>Gets or sets the next fragment of a tool call's arguments, present on a <c>toolUse</c> delta.</summary>
    [JsonPropertyName("toolUse")]
    public AwsBedrockToolUseBlockDeltaDto? ToolUse { get; set; }
}
