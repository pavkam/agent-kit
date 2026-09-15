// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Http;

/// <summary>
/// Resolves a provider-neutral <see cref="ProviderCredential"/> into the
/// concrete HTTP authentication header a provider expects, according to that
/// provider's <see cref="ProviderAuthorizationScheme"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the one place in the first-party HTTP adapters that
/// distinguishes API-key authentication from OAuth bearer-token
/// authentication. An <see cref="ApiKeyProviderCredential"/> is treated as
/// valid for as long as the application configured it and is mapped through
/// the scheme's <see cref="ProviderAuthorizationScheme.ApiKeyHeaderName"/>
/// and <see cref="ProviderAuthorizationScheme.ApiKeyValuePrefix"/>, or
/// denied when the scheme has no API-key mode. An
/// <see cref="OAuthTokenProviderCredential"/> is always sent as
/// <c>Authorization: Bearer &lt;token&gt;</c>, and is rejected here, before
/// any network call, once its
/// <see cref="OAuthTokenProviderCredential.ExpiresAtUtc"/> is at or before
/// the injected clock's current instant.
/// </para>
/// <para>
/// This factory does not perform an OAuth token-acquisition or refresh
/// flow; the application (or a dedicated identity package) already owns
/// producing a current <see cref="OAuthTokenProviderCredential"/> through
/// its registered <see cref="IProviderCredentialSource"/>. This factory
/// only decides whether the credential it was handed is still usable and,
/// if so, how to express it on the wire. Denial messages name the
/// credential's CLR type and the provider identity; they never contain
/// credential material.
/// </para>
/// <para>
/// This type is stateless and safe to call concurrently.
/// </para>
/// </remarks>
public static class ProviderAuthorizationHeaderFactory
{
    private const string _bearerHeaderName = "Authorization";
    private const string _bearerPrefix = "Bearer ";

    /// <summary>
    /// Resolves <paramref name="credential"/> into a
    /// <see cref="ProviderAuthorizationResult"/> for <paramref name="providerId"/>
    /// using <paramref name="scheme"/>.
    /// </summary>
    /// <param name="credential">The credential to resolve.</param>
    /// <param name="providerId">The provider the request will be sent to, used for diagnostics.</param>
    /// <param name="timeProvider">The clock used to evaluate OAuth token expiry.</param>
    /// <param name="scheme">The provider's verified API-key header shape.</param>
    /// <returns>
    /// A <see cref="ProviderAuthorizationGranted"/> carrying a usable
    /// authentication header, or a <see cref="ProviderAuthorizationDenied"/>
    /// carrying a typed <see cref="ProviderFailureKind.Authentication"/>
    /// failure for an expired token, an API key the scheme does not
    /// accept, or an unrecognized credential kind.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="credential"/>, <paramref name="timeProvider"/>, or
    /// <paramref name="scheme"/> is <see langword="null"/>.
    /// </exception>
    public static ProviderAuthorizationResult Create(
        ProviderCredential credential,
        ProviderId providerId,
        TimeProvider timeProvider,
        ProviderAuthorizationScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(scheme);

        return credential switch
        {
            ApiKeyProviderCredential apiKey => CreateFromApiKey(apiKey, providerId, scheme),
            OAuthTokenProviderCredential oauthToken => CreateFromOAuthToken(oauthToken, providerId, timeProvider),
            _ => Deny(
                providerId,
                $"Credential kind '{credential.GetType().Name}' is not supported by the '{providerId}' provider integration."),
        };
    }

    private static ProviderAuthorizationResult CreateFromApiKey(
        ApiKeyProviderCredential apiKey,
        ProviderId providerId,
        ProviderAuthorizationScheme scheme)
    {
        Debug.Assert(apiKey is not null, "Caller matched a non-null API-key credential.");
        Debug.Assert(scheme is not null, "Caller validated the scheme.");

        return scheme.ApiKeyHeaderName is { } headerName
            ? new ProviderAuthorizationGranted(headerName, scheme.ApiKeyValuePrefix + apiKey.ApiKey)
            : Deny(
                providerId,
                $"Credential kind '{nameof(ApiKeyProviderCredential)}' is not supported by the '{providerId}' provider " +
                "integration; this provider has no API-key authentication mode and requires an OAuth access token.");
    }

    private static ProviderAuthorizationResult CreateFromOAuthToken(
        OAuthTokenProviderCredential oauthToken,
        ProviderId providerId,
        TimeProvider timeProvider)
    {
        Debug.Assert(oauthToken is not null, "Caller matched a non-null OAuth credential.");
        Debug.Assert(timeProvider is not null, "Caller validated the clock.");

        return oauthToken.ExpiresAtUtc is { } expiresAtUtc && expiresAtUtc <= timeProvider.GetUtcNow()
            ? Deny(providerId, "The configured OAuth access token has expired.")
            : new ProviderAuthorizationGranted(_bearerHeaderName, _bearerPrefix + oauthToken.AccessToken);
    }

    private static ProviderAuthorizationDenied Deny(ProviderId providerId, string message)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(message), "Denial messages are authored constants.");

        return new ProviderAuthorizationDenied(
            new ProviderFailure(
                ProviderFailureKind.Authentication,
                providerId,
                requestId: null,
                statusCode: null,
                providerCode: null,
                retryAfter: null,
                message,
                diagnosticCause: null,
                ExtensionData.Empty));
    }
}
