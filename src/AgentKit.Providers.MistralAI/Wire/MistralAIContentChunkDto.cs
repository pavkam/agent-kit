// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Wire;

/// <summary>
/// The wire shape of one entry in a message or delta <c>content</c> array,
/// covering the fields used by a <c>TextChunk</c>. Other content-chunk
/// kinds (<c>image_url</c>, <c>document_url</c>, <c>reference</c>,
/// <c>file</c>, <c>thinking</c>, <c>input_audio</c>) are not translated and
/// are captured only through <see cref="ExtensionData"/> so they can be
/// preserved as an <see cref="UnknownContentPart"/> instead of silently
/// dropped.
/// </summary>
internal sealed class MistralAIContentChunkDto
{
    /// <summary>Gets or sets the chunk type discriminator, such as <c>"text"</c> or <c>"thinking"</c>.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Gets or sets the visible text, present on a <c>text</c> chunk.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>Gets or sets any fields not modeled above.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
