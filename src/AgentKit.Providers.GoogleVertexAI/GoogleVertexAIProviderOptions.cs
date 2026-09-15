// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI;

/// <summary>
/// Configures the Google Vertex AI integration's project, location,
/// optional explicit service endpoint, and wire-behavior defaults.
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
    /// Gets or sets the Google Cloud location hosting the request, such as
    /// the region <c>us-central1</c> or the multi-region
    /// <see cref="GoogleVertexAIProviderDefaults.GlobalLocation"/>, or
    /// <see langword="null"/> if not yet configured.
    /// </summary>
    /// <remarks>
    /// The location is always embedded in the resource path
    /// (<c>projects/{project}/locations/{location}/...</c>). Unless
    /// <see cref="BaseAddress"/> is set, it also selects the service host:
    /// a region resolves to <c>https://{location}-aiplatform.googleapis.com/</c>
    /// and <c>global</c> resolves to <c>https://aiplatform.googleapis.com/</c>.
    /// </remarks>
    public string? Location { get; set; }

    /// <summary>
    /// Gets or sets an explicit Vertex AI service base address, or
    /// <see langword="null"/> to derive the host from <see cref="Location"/>
    /// through <see cref="GoogleVertexAIProviderDefaults.BuildDefaultBaseAddress"/>.
    /// </summary>
    /// <remarks>
    /// Set this to target a Private Service Connect endpoint, a corporate
    /// proxy, a multi-region endpoint such as
    /// <c>https://aiplatform.us.rep.googleapis.com/</c>, or a loopback test
    /// server. The value must be an absolute URI when set; it replaces only
    /// the scheme, host, and port, while the API version and
    /// project/location/publisher resource path are still appended by the
    /// URI builders. <see cref="Location"/> remains required because it is
    /// part of the resource name regardless of which host serves it.
    /// </remarks>
    public Uri? BaseAddress { get; set; }

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
