// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

using System.Net.Http.Headers;

/// <summary>
/// Resolves a provider-neutral <see cref="ProviderCredential"/> into the
/// concrete HTTP <c>Authorization</c> header OpenAI-compatible endpoints
/// expect.
/// </summary>
/// <remarks>
/// <para>
/// This is the one place in the OpenAI integration that distinguishes API
/// key authentication from OAuth bearer token authentication, because the
/// core AgentKit architecture deliberately leaves that distinction to each
/// concrete provider integration: <see cref="ApiKeyProviderCredential"/>
/// and <see cref="OAuthTokenProviderCredential"/> are both, at the wire
/// level, a <c>Bearer</c>-scheme <c>Authorization</c> header for OpenAI's
/// REST API, but they differ in validity: an API key is treated as valid
/// for as long as the application configured it, while an OAuth token
/// carries its own <see cref="OAuthTokenProviderCredential.ExpiresAtUtc"/>
/// and is rejected here, before any network call, once that instant has
/// passed.
/// </para>
/// <para>
/// This factory does not perform an OAuth token-acquisition or refresh
/// flow; the application (or a dedicated identity package) already owns
/// producing a current <see cref="OAuthTokenProviderCredential"/> through
/// its registered <see cref="IProviderCredentialSource"/>. This factory
/// only decides whether the credential it was handed is still usable and,
/// if so, how to express it on the wire.
/// </para>
/// </remarks>
public static class OpenAIAuthorizationHeaderFactory
{
    private const string _bearerScheme = "Bearer";

    /// <summary>
    /// Resolves <paramref name="credential"/> into an
    /// <see cref="OpenAIAuthorizationResult"/> for <paramref name="providerId"/>.
    /// </summary>
    /// <param name="credential">The credential to resolve.</param>
    /// <param name="providerId">The provider the request will be sent to, used for diagnostics.</param>
    /// <param name="timeProvider">The clock used to evaluate OAuth token expiry.</param>
    /// <returns>
    /// An <see cref="OpenAIAuthorizationGranted"/> carrying a usable
    /// <c>Authorization</c> header, or an <see cref="OpenAIAuthorizationDenied"/>
    /// carrying a typed <see cref="ProviderFailureKind.Authentication"/>
    /// failure.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="credential"/> or <paramref name="timeProvider"/> is
    /// null.
    /// </exception>
    public static OpenAIAuthorizationResult Create(
        ProviderCredential credential,
        ProviderId providerId,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return credential switch
        {
            ApiKeyProviderCredential apiKey =>
                new OpenAIAuthorizationGranted(new AuthenticationHeaderValue(_bearerScheme, apiKey.ApiKey)),

            OAuthTokenProviderCredential oauthToken => CreateFromOAuthToken(oauthToken, providerId, timeProvider),

            _ => new OpenAIAuthorizationDenied(
                new ProviderFailure(
                    ProviderFailureKind.Authentication,
                    providerId,
                    requestId: null,
                    statusCode: null,
                    providerCode: null,
                    retryAfter: null,
                    $"Credential kind '{credential.GetType().Name}' is not supported by the OpenAI-compatible integration.",
                    diagnosticCause: null,
                    ExtensionData.Empty)),
        };
    }

    private static OpenAIAuthorizationResult CreateFromOAuthToken(
        OAuthTokenProviderCredential oauthToken,
        ProviderId providerId,
        TimeProvider timeProvider)
    {
        return oauthToken.ExpiresAtUtc is { } expiresAtUtc && expiresAtUtc <= timeProvider.GetUtcNow()
            ? new OpenAIAuthorizationDenied(
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
            : new OpenAIAuthorizationGranted(new AuthenticationHeaderValue(_bearerScheme, oauthToken.AccessToken));
    }
}
