// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI;

/// <summary>
/// Resolves a provider-neutral <see cref="ProviderCredential"/> into the
/// concrete HTTP authentication header the Vertex AI REST API expects.
/// </summary>
/// <remarks>
/// Vertex AI documents exactly one authentication mechanism:
/// <c>Authorization: Bearer ACCESS_TOKEN</c>, using Google OAuth 2.0 or
/// Application Default Credentials with the relevant
/// <c>aiplatform.*</c> IAM permissions. Unlike the Gemini Developer API,
/// Vertex has no API-key authentication mode at all, so a supplied
/// <see cref="ApiKeyProviderCredential"/> is always denied rather than
/// mapped to any header. An OAuth token past its
/// <see cref="OAuthTokenProviderCredential.ExpiresAtUtc"/> is rejected
/// here, before any network call.
/// </remarks>
public static class GoogleVertexAIAuthorizationHeaderFactory
{
    private const string _bearerHeaderName = "Authorization";
    private const string _bearerScheme = "Bearer";

    /// <summary>
    /// Resolves <paramref name="credential"/> into a
    /// <see cref="GoogleVertexAIAuthorizationResult"/> for <paramref name="providerId"/>.
    /// </summary>
    /// <param name="credential">The credential to resolve.</param>
    /// <param name="providerId">The provider the request will be sent to, used for diagnostics.</param>
    /// <param name="timeProvider">The clock used to evaluate OAuth token expiry.</param>
    /// <returns>
    /// A <see cref="GoogleVertexAIAuthorizationGranted"/> carrying a usable
    /// authentication header, or a <see cref="GoogleVertexAIAuthorizationDenied"/>
    /// carrying a typed <see cref="ProviderFailureKind.Authentication"/>
    /// failure.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="credential"/> or <paramref name="timeProvider"/> is
    /// null.
    /// </exception>
    public static GoogleVertexAIAuthorizationResult Create(
        ProviderCredential credential,
        ProviderId providerId,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return credential switch
        {
            OAuthTokenProviderCredential oauthToken => CreateFromOAuthToken(oauthToken, providerId, timeProvider),

            _ => new GoogleVertexAIAuthorizationDenied(
                new ProviderFailure(
                    ProviderFailureKind.Authentication,
                    providerId,
                    requestId: null,
                    statusCode: null,
                    providerCode: null,
                    retryAfter: null,
                    $"Credential kind '{credential.GetType().Name}' is not supported by the Google Vertex AI " +
                    "integration; Vertex AI requires an OAuth access token from Google Cloud Application " +
                    "Default Credentials, not an API key.",
                    diagnosticCause: null,
                    ExtensionData.Empty)),
        };
    }

    private static GoogleVertexAIAuthorizationResult CreateFromOAuthToken(
        OAuthTokenProviderCredential oauthToken,
        ProviderId providerId,
        TimeProvider timeProvider)
    {
        return oauthToken.ExpiresAtUtc is { } expiresAtUtc && expiresAtUtc <= timeProvider.GetUtcNow()
            ? new GoogleVertexAIAuthorizationDenied(
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
            : new GoogleVertexAIAuthorizationGranted(_bearerHeaderName, $"{_bearerScheme} {oauthToken.AccessToken}");
    }
}
