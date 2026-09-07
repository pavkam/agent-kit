// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI;

/// <summary>
/// Configures the Google Vertex AI integration's project, region, and
/// wire-behavior defaults.
/// </summary>
/// <remarks>
/// This is a mutable options class following the standard
/// <c>Microsoft.Extensions.Options</c> pattern; it is bound and validated
/// once at composition time and consumed as an immutable
/// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/> value
/// afterward. Authentication is configured separately, through
/// <c>AddGoogleVertexAIOAuthCredential</c>, because credential material is
/// never appropriate to bind from ordinary configuration alongside
/// endpoint options. Unlike the Gemini Developer API, a Vertex AI project
/// and region are account-specific, so <see cref="ProjectId"/> and
/// <see cref="Location"/> have no fabricated default; they must be
/// configured explicitly.
/// </remarks>
public sealed class GoogleVertexAIProviderOptions
{
    /// <summary>
    /// Gets or sets the caller's Google Cloud project ID, or
    /// <see langword="null"/> if not yet configured.
    /// </summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the Google Cloud region hosting the request, such as
    /// <c>us-central1</c>, or <see langword="null"/> if not yet
    /// configured.
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Gets or sets the publisher namespace of the requested model, such
    /// as <c>google</c> for Gemini models.
    /// </summary>
    public string Publisher { get; set; } = GoogleVertexAIProviderDefaults.DefaultPublisher;

    /// <summary>Gets or sets the REST API version path segment.</summary>
    public string ApiVersion { get; set; } = GoogleVertexAIProviderDefaults.DefaultApiVersion;

    /// <summary>
    /// Gets or sets whether a request should prefer the streaming
    /// (<c>:streamGenerateContent</c>) operation when the selected model
    /// supports streaming.
    /// </summary>
    public bool PreferStreaming { get; set; } = true;
}
