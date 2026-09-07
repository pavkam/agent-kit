// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Wire;

/// <summary>
/// The wire shape of one Converse <c>ContentBlock</c> union entry, covering
/// the <c>text</c> and <c>toolUse</c> members this package translates.
/// Other union members (<c>image</c>, <c>document</c>, <c>video</c>,
/// <c>guardContent</c>, <c>reasoningContent</c>, <c>citationsContent</c>,
/// <c>cachePoint</c>, <c>searchResult</c>, <c>audio</c>) are not yet
/// translated and are captured only through <see cref="ExtensionData"/> so
/// they can be preserved as an <see cref="UnknownContentPart"/> instead of
/// silently dropped.
/// </summary>
internal sealed class AwsBedrockContentBlockDto
{
    /// <summary>Gets or sets the visible text, present on a <c>text</c> block.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>Gets or sets the tool call request, present on a <c>toolUse</c> block.</summary>
    [JsonPropertyName("toolUse")]
    public AwsBedrockToolUseBlockDto? ToolUse { get; set; }

    /// <summary>Gets or sets any fields not modeled above.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
