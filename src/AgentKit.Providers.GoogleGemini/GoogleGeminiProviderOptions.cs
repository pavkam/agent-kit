// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini;

/// <summary>
/// Configures the Google Gemini integration's endpoint and wire-behavior
/// defaults.
/// </summary>
/// <remarks>
/// This is a mutable options class following the standard
/// <c>Microsoft.Extensions.Options</c> pattern; it is bound and validated
/// once at composition time and consumed as an immutable
/// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/> value
/// afterward. Authentication is configured separately, through
/// <c>AddGoogleGeminiApiKeyCredential</c> or
/// <c>AddGoogleGeminiOAuthCredential</c>, because credential material is
/// never appropriate to bind from ordinary configuration alongside
/// endpoint options.
/// </remarks>
public sealed class GoogleGeminiProviderOptions
{
    /// <summary>Gets or sets the base address of the Gemini Developer API.</summary>
    public Uri BaseAddress { get; set; } = GoogleGeminiProviderDefaults.DefaultBaseAddress;

    /// <summary>
    /// Gets or sets the API version path segment (for example,
    /// <c>"v1beta"</c>) used to build the operation URI.
    /// </summary>
    public string ApiVersion { get; set; } = GoogleGeminiProviderDefaults.DefaultApiVersion;

    /// <summary>
    /// Gets or sets whether a request should prefer the streaming
    /// (<c>:streamGenerateContent</c>) operation when the selected model
    /// supports streaming.
    /// </summary>
    public bool PreferStreaming { get; set; } = true;
}
