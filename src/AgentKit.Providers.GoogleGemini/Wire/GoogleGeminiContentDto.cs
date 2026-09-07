// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Wire;

/// <summary>
/// The wire shape of a Gemini <c>Content</c> object, carried by a
/// candidate's committed content.
/// </summary>
internal sealed class GoogleGeminiContentDto
{
    /// <summary>Gets or sets the content role, normally <c>"model"</c> for a response.</summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>Gets or sets the ordered content parts.</summary>
    [JsonPropertyName("parts")]
    public IReadOnlyList<GoogleGeminiPartDto>? Parts { get; set; }
}
