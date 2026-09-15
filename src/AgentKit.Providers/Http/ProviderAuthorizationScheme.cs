// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Http;

/// <summary>
/// Describes how one HTTP provider expresses a static API key on the wire,
/// which is the only per-provider variation in first-party header
/// authentication.
/// </summary>
/// <remarks>
/// <para>
/// Every first-party HTTP adapter sends an
/// <see cref="OAuthTokenProviderCredential"/> as
/// <c>Authorization: Bearer &lt;token&gt;</c>; that is fixed by
/// <see cref="ProviderAuthorizationHeaderFactory"/> and not configurable
/// here. Providers differ only in how they accept an
/// <see cref="ApiKeyProviderCredential"/>: some reuse the bearer form
/// (<see cref="BearerToken"/>), some use a dedicated header with the bare
/// key as its value (<see cref="ForApiKeyHeader"/>), and some offer no
/// API-key mode at all (<see cref="OAuthTokenOnly"/>).
/// </para>
/// <para>
/// This type is an immutable value object with structural equality. Each
/// provider package exposes its verified scheme as a constant on its
/// <c>&lt;Provider&gt;ProviderDefaults</c> type; an application does not
/// normally construct one.
/// </para>
/// </remarks>
public sealed record ProviderAuthorizationScheme
{
    private const string _authorizationHeaderName = "Authorization";
    private const string _bearerPrefix = "Bearer ";

    /// <summary>Initializes a new instance of the <see cref="ProviderAuthorizationScheme"/> record.</summary>
    /// <param name="apiKeyHeaderName">
    /// The header that carries a static API key, or <see langword="null"/>
    /// when the provider accepts no API-key credential and every
    /// <see cref="ApiKeyProviderCredential"/> must be denied.
    /// </param>
    /// <param name="apiKeyValuePrefix">
    /// The text prepended to the API key inside the header value, such as
    /// <c>"Bearer "</c>; <see cref="string.Empty"/> when the header carries
    /// the bare key. Ignored when <paramref name="apiKeyHeaderName"/> is
    /// <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="apiKeyHeaderName"/> is non-null but empty or
    /// consists only of whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="apiKeyValuePrefix"/> is <see langword="null"/>.</exception>
    public ProviderAuthorizationScheme(string? apiKeyHeaderName, string apiKeyValuePrefix)
    {
        if (apiKeyHeaderName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(apiKeyHeaderName);
        }

        ArgumentNullException.ThrowIfNull(apiKeyValuePrefix);

        ApiKeyHeaderName = apiKeyHeaderName;
        ApiKeyValuePrefix = apiKeyValuePrefix;
    }

    /// <summary>
    /// Gets the scheme used by providers that accept an API key in the same
    /// <c>Authorization: Bearer &lt;key&gt;</c> form as an OAuth token, such
    /// as OpenAI-compatible endpoints, Cohere, and Mistral AI.
    /// </summary>
    public static ProviderAuthorizationScheme BearerToken { get; } = new(_authorizationHeaderName, _bearerPrefix);

    /// <summary>
    /// Gets the scheme used by providers that have no API-key authentication
    /// mode, such as Google Vertex AI. Every
    /// <see cref="ApiKeyProviderCredential"/> is denied before any request is
    /// sent; only an <see cref="OAuthTokenProviderCredential"/> is accepted.
    /// </summary>
    public static ProviderAuthorizationScheme OAuthTokenOnly { get; } = new(apiKeyHeaderName: null, string.Empty);

    /// <summary>
    /// Gets the header that carries a static API key, or
    /// <see langword="null"/> when the provider accepts no API key.
    /// </summary>
    public string? ApiKeyHeaderName { get; init; }

    /// <summary>
    /// Gets the text prepended to the API key inside the header value;
    /// <see cref="string.Empty"/> when the header carries the bare key.
    /// </summary>
    public string ApiKeyValuePrefix { get; init; }

    /// <summary>
    /// Gets a value indicating whether the provider accepts an
    /// <see cref="ApiKeyProviderCredential"/> at all.
    /// </summary>
    /// <value><see langword="true"/> when <see cref="ApiKeyHeaderName"/> is non-null.</value>
    public bool SupportsApiKey => ApiKeyHeaderName is not null;

    /// <summary>
    /// Creates the scheme for a provider that sends a bare API key in a
    /// dedicated header with no scheme prefix, such as Anthropic's
    /// <c>x-api-key</c>, Google Gemini's <c>x-goog-api-key</c>, or Azure
    /// OpenAI's <c>api-key</c>.
    /// </summary>
    /// <param name="headerName">The name of the API-key header.</param>
    /// <returns>A scheme whose <see cref="ApiKeyHeaderName"/> is <paramref name="headerName"/> and whose <see cref="ApiKeyValuePrefix"/> is empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="headerName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="headerName"/> is empty or consists only of whitespace.</exception>
    public static ProviderAuthorizationScheme ForApiKeyHeader(string headerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        return new ProviderAuthorizationScheme(headerName, string.Empty);
    }
}
