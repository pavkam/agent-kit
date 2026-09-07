// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Wire;

/// <summary>
/// The wire shape of a complete <c>GenerateContentResponse</c> body,
/// whether returned in full by <c>:generateContent</c> or repeated as a
/// growing partial object by each <c>:streamGenerateContent</c> SSE chunk.
/// </summary>
internal sealed class GoogleGeminiGenerateContentResponseDto
{
    /// <summary>Gets or sets the ordered response candidates. Only the first is used.</summary>
    [JsonPropertyName("candidates")]
    public IReadOnlyList<GoogleGeminiCandidateDto>? Candidates { get; set; }

    /// <summary>Gets or sets feedback about why the prompt itself was blocked, when it was.</summary>
    [JsonPropertyName("promptFeedback")]
    public GoogleGeminiPromptFeedbackDto? PromptFeedback { get; set; }

    /// <summary>Gets or sets token usage for the request, when reported at this point.</summary>
    [JsonPropertyName("usageMetadata")]
    public GoogleGeminiUsageMetadataDto? UsageMetadata { get; set; }

    /// <summary>Gets or sets the resolved model version that actually served the request.</summary>
    [JsonPropertyName("modelVersion")]
    public string? ModelVersion { get; set; }

    /// <summary>Gets or sets the provider-supplied response identifier.</summary>
    [JsonPropertyName("responseId")]
    public string? ResponseId { get; set; }
}
