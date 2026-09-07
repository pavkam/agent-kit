// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Wire;

/// <summary>
/// The wire shape of a Converse <c>ToolUseBlock</c>, the complete
/// (non-streamed) representation of a tool call request.
/// </summary>
internal sealed class AwsBedrockToolUseBlockDto
{
    /// <summary>Gets or sets the provider-supplied tool call identifier.</summary>
    [JsonPropertyName("toolUseId")]
    public string? ToolUseId { get; set; }

    /// <summary>Gets or sets the tool/function name the model chose to call.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Gets or sets the parsed call argument object.</summary>
    [JsonPropertyName("input")]
    public JsonElement? Input { get; set; }
}
