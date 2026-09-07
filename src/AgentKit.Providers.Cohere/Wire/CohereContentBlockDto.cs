// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Wire;

/// <summary>
/// The wire shape of one assistant-message content block, covering the
/// fields used by a <c>text</c> or <c>thinking</c> block. This shape is
/// reused for a buffered response's <c>message.content[]</c> entries and
/// for the singular <c>delta.message.content</c> object carried by a
/// streaming <c>content-start</c> or <c>content-delta</c> event. Other
/// content-block kinds are not translated and are captured only through
/// <see cref="ExtensionData"/> so they can be preserved as an
/// <see cref="UnknownContentPart"/> instead of silently dropped.
/// </summary>
internal sealed class CohereContentBlockDto
{
    /// <summary>Gets or sets the content-block type discriminator, such as <c>"text"</c> or <c>"thinking"</c>.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Gets or sets the visible text, present on a <c>text</c> block.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>Gets or sets the reasoning text, present on a <c>thinking</c> block.</summary>
    [JsonPropertyName("thinking")]
    public string? Thinking { get; set; }

    /// <summary>Gets or sets any fields not modeled above.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
