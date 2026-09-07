// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere;

/// <summary>
/// Resolves a provider-neutral <see cref="ProviderCredential"/> into the
/// concrete HTTP authentication header the Cohere API expects.
/// </summary>
/// <remarks>
/// Cohere documents exactly one authentication mechanism:
/// <c>Authorization: Bearer COHERE_API_KEY</c>. Both an
/// <see cref="ApiKeyProviderCredential"/> and an
/// <see cref="OAuthTokenProviderCredential"/> are sent through that same
/// header shape; an OAuth token past its
/// <see cref="OAuthTokenProviderCredential.ExpiresAtUtc"/> is rejected
/// here, before any network call.
/// </remarks>
public static class CohereAuthorizationHeaderFactory
{
    private const string _authorizationHeaderName = "Authorization";
    private const string _bearerScheme = "Bearer";

    /// <summary>
    /// Resolves <paramref name="credential"/> into a
    /// <see cref="CohereAuthorizationResult"/> for <paramref name="providerId"/>.
    /// </summary>
    /// <param name="credential">The credential to resolve.</param>
    /// <param name="providerId">The provider the request will be sent to, used for diagnostics.</param>
    /// <param name="timeProvider">The clock used to evaluate OAuth token expiry.</param>
    /// <returns>
    /// A <see cref="CohereAuthorizationGranted"/> carrying a usable
    /// authentication header, or a <see cref="CohereAuthorizationDenied"/>
    /// carrying a typed <see cref="ProviderFailureKind.Authentication"/>
    /// failure.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="credential"/> or <paramref name="timeProvider"/> is
    /// null.
    /// </exception>
    public static CohereAuthorizationResult Create(
        ProviderCredential credential,
        ProviderId providerId,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return credential switch
        {
            ApiKeyProviderCredential apiKey =>
                new CohereAuthorizationGranted(_authorizationHeaderName, $"{_bearerScheme} {apiKey.ApiKey}"),

            OAuthTokenProviderCredential oauthToken => CreateFromOAuthToken(oauthToken, providerId, timeProvider),

            _ => new CohereAuthorizationDenied(
                new ProviderFailure(
                    ProviderFailureKind.Authentication,
                    providerId,
                    requestId: null,
                    statusCode: null,
                    providerCode: null,
                    retryAfter: null,
                    $"Credential kind '{credential.GetType().Name}' is not supported by the Cohere integration.",
                    diagnosticCause: null,
                    ExtensionData.Empty)),
        };
    }

    private static CohereAuthorizationResult CreateFromOAuthToken(
        OAuthTokenProviderCredential oauthToken,
        ProviderId providerId,
        TimeProvider timeProvider)
    {
        return oauthToken.ExpiresAtUtc is { } expiresAtUtc && expiresAtUtc <= timeProvider.GetUtcNow()
            ? new CohereAuthorizationDenied(
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
            : new CohereAuthorizationGranted(_authorizationHeaderName, $"{_bearerScheme} {oauthToken.AccessToken}");
    }
}
