// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini;

/// <summary>
/// Resolves a provider-neutral <see cref="ProviderCredential"/> into the
/// concrete HTTP authentication header the Gemini Developer API expects.
/// </summary>
/// <remarks>
/// This is the one place in the Gemini integration that distinguishes API
/// key authentication from OAuth bearer-token authentication, because the
/// core AgentKit architecture deliberately leaves that distinction to each
/// concrete provider integration. An <see cref="ApiKeyProviderCredential"/>
/// is sent as <c>x-goog-api-key: &lt;key&gt;</c>, matching the Gemini
/// Developer API's documented header authentication (preferred over the
/// equivalent <c>?key=</c> query parameter so credentials never appear in
/// URLs or logs). An <see cref="OAuthTokenProviderCredential"/> is sent as
/// a standard <c>Authorization: Bearer &lt;token&gt;</c> header instead. An
/// OAuth token past its <see cref="OAuthTokenProviderCredential.ExpiresAtUtc"/>
/// is rejected here, before any network call.
/// </remarks>
public static class GoogleGeminiAuthorizationHeaderFactory
{
    private const string _apiKeyHeaderName = "x-goog-api-key";
    private const string _bearerHeaderName = "Authorization";
    private const string _bearerScheme = "Bearer";

    /// <summary>
    /// Resolves <paramref name="credential"/> into a
    /// <see cref="GoogleGeminiAuthorizationResult"/> for <paramref name="providerId"/>.
    /// </summary>
    /// <param name="credential">The credential to resolve.</param>
    /// <param name="providerId">The provider the request will be sent to, used for diagnostics.</param>
    /// <param name="timeProvider">The clock used to evaluate OAuth token expiry.</param>
    /// <returns>
    /// A <see cref="GoogleGeminiAuthorizationGranted"/> carrying a usable
    /// authentication header, or a <see cref="GoogleGeminiAuthorizationDenied"/>
    /// carrying a typed <see cref="ProviderFailureKind.Authentication"/>
    /// failure.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="credential"/> or <paramref name="timeProvider"/> is
    /// null.
    /// </exception>
    public static GoogleGeminiAuthorizationResult Create(
        ProviderCredential credential,
        ProviderId providerId,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return credential switch
        {
            ApiKeyProviderCredential apiKey =>
                new GoogleGeminiAuthorizationGranted(_apiKeyHeaderName, apiKey.ApiKey),

            OAuthTokenProviderCredential oauthToken => CreateFromOAuthToken(oauthToken, providerId, timeProvider),

            _ => new GoogleGeminiAuthorizationDenied(
                new ProviderFailure(
                    ProviderFailureKind.Authentication,
                    providerId,
                    requestId: null,
                    statusCode: null,
                    providerCode: null,
                    retryAfter: null,
                    $"Credential kind '{credential.GetType().Name}' is not supported by the Google Gemini integration.",
                    diagnosticCause: null,
                    ExtensionData.Empty)),
        };
    }

    private static GoogleGeminiAuthorizationResult CreateFromOAuthToken(
        OAuthTokenProviderCredential oauthToken,
        ProviderId providerId,
        TimeProvider timeProvider)
    {
        return oauthToken.ExpiresAtUtc is { } expiresAtUtc && expiresAtUtc <= timeProvider.GetUtcNow()
            ? new GoogleGeminiAuthorizationDenied(
                new ProviderFailure(
                    ProviderFailureKind.Authentication,
                    providerId,
                    requestId: null,
                    statusCode: null,
                    providerCode: null,
                    retryAfter: null,
                    "The configured OAuth access token has expired.",
                    diagnosticCause: null,
                    ExtensionData.Empty))
            : new GoogleGeminiAuthorizationGranted(_bearerHeaderName, $"{_bearerScheme} {oauthToken.AccessToken}");
    }
}
