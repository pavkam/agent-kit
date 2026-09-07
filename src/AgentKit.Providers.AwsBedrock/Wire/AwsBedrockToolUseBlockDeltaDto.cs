// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Wire;

/// <summary>
/// The wire shape of a streaming <c>ToolUseBlockDelta</c>, carried by a
/// <c>contentBlockDelta</c> event's <c>delta.toolUse</c> field. The
/// <see cref="Input"/> value is a fragment of a JSON document's text
/// representation, not a parsed value; fragments for one content block
/// index must be concatenated before parsing.
/// </summary>
internal sealed class AwsBedrockToolUseBlockDeltaDto
{
    /// <summary>Gets or sets the next fragment of the call argument JSON text.</summary>
    [JsonPropertyName("input")]
    public string? Input { get; set; }
}
