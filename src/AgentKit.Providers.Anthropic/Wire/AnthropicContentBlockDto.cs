// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic.Wire;

/// <summary>
/// The wire shape of one Anthropic Messages content block, covering the
/// union of fields used by <c>text</c>, <c>tool_use</c>, <c>thinking</c>,
/// and <c>redacted_thinking</c> blocks. Only the fields matching
/// <see cref="Type"/> are populated; the others remain null.
/// </summary>
/// <remarks>
/// This flexible envelope is used both for entries in a committed
/// <c>Message.content</c> array and for the nested <c>content_block</c>
/// object of a streaming <c>content_block_start</c> event, since both use
/// the identical per-kind field set.
/// </remarks>
internal sealed class AnthropicContentBlockDto
{
    /// <summary>Gets or sets the content block kind discriminator.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>Gets or sets the visible text, present on <c>text</c> blocks.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>Gets or sets the provider-supplied tool call identifier, present on <c>tool_use</c> blocks.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Gets or sets the tool name, present on <c>tool_use</c> blocks.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Gets or sets the parsed tool call input object, present on <c>tool_use</c> blocks.</summary>
    [JsonPropertyName("input")]
    public JsonElement? Input { get; set; }

    /// <summary>Gets or sets the visible reasoning text, present on <c>thinking</c> blocks.</summary>
    [JsonPropertyName("thinking")]
    public string? Thinking { get; set; }

    /// <summary>
    /// Gets or sets the opaque provider signature, present on <c>thinking</c>
    /// blocks and required to echo a thinking block back on a continuation
    /// request.
    /// </summary>
    [JsonPropertyName("signature")]
    public string? Signature { get; set; }

    /// <summary>
    /// Gets or sets the opaque redacted reasoning payload, present on
    /// <c>redacted_thinking</c> blocks in place of visible text.
    /// </summary>
    [JsonPropertyName("data")]
    public string? Data { get; set; }

    /// <summary>
    /// Gets or sets any fields not modeled above, captured so a future or
    /// unrecognized block kind (for example, an image, document, citation,
    /// or server-tool block this package does not yet translate) can be
    /// preserved as an <see cref="UnknownContentPart"/> instead of silently
    /// dropped.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
