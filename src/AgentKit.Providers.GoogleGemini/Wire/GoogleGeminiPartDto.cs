// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Wire;

/// <summary>
/// The wire shape of one Gemini <c>Content.parts[]</c> entry, covering the
/// union of fields used by text, thought, and function-call parts. Only the
/// fields matching the part's actual kind are populated; the others remain
/// null.
/// </summary>
internal sealed class GoogleGeminiPartDto
{
    /// <summary>Gets or sets the visible or reasoning text, present on text and thought parts.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>
    /// Gets or sets whether this part is model reasoning ("thought")
    /// rather than visible output text.
    /// </summary>
    [JsonPropertyName("thought")]
    public bool? Thought { get; set; }

    /// <summary>
    /// Gets or sets the opaque provider signature for a thought part,
    /// required to echo the thought back on a continuation request.
    /// </summary>
    [JsonPropertyName("thoughtSignature")]
    public string? ThoughtSignature { get; set; }

    /// <summary>Gets or sets the requested function call, present on a model-authored function-call part.</summary>
    [JsonPropertyName("functionCall")]
    public GoogleGeminiFunctionCallDto? FunctionCall { get; set; }

    /// <summary>
    /// Gets or sets any fields not modeled above, captured so a future or
    /// unrecognized part kind (for example, inline/file media or
    /// executable-code parts this package does not yet translate) can be
    /// preserved as an <see cref="UnknownContentPart"/> instead of silently
    /// dropped.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
