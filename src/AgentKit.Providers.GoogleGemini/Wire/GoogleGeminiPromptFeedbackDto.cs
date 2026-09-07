// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Wire;

/// <summary>
/// The wire shape of the <c>promptFeedback</c> object in a
/// <c>GenerateContentResponse</c>, reported when the prompt itself was
/// blocked before any candidate could be produced.
/// </summary>
internal sealed class GoogleGeminiPromptFeedbackDto
{
    /// <summary>Gets or sets the reason the prompt was blocked, when it was.</summary>
    [JsonPropertyName("blockReason")]
    public string? BlockReason { get; set; }
}
