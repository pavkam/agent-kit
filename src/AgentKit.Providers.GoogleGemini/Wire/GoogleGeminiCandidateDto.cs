// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Wire;

/// <summary>
/// The wire shape of one entry in a <c>GenerateContentResponse.candidates</c>
/// array. Only the first candidate is used; AgentKit's <see cref="ILlmModel"/>
/// contract does not model multiple concurrent response candidates.
/// </summary>
internal sealed class GoogleGeminiCandidateDto
{
    /// <summary>Gets or sets the committed or in-progress content for this candidate.</summary>
    [JsonPropertyName("content")]
    public GoogleGeminiContentDto? Content { get; set; }

    /// <summary>Gets or sets the provider's raw, unnormalized stop reason text.</summary>
    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; set; }

    /// <summary>Gets or sets the zero-based candidate index.</summary>
    [JsonPropertyName("index")]
    public int? Index { get; set; }
}
