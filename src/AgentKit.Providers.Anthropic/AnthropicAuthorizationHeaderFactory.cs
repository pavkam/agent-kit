// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic;

/// <summary>
/// Resolves a provider-neutral <see cref="ProviderCredential"/> into the
/// concrete HTTP authentication header the Anthropic Messages API expects.
/// </summary>
/// <remarks>
/// This is the one place in the Anthropic integration that distinguishes
/// API key authentication from OAuth bearer-token authentication, because
/// the core AgentKit architecture deliberately leaves that distinction to
/// each concrete provider integration. Unlike OpenAI-compatible endpoints,
/// Anthropic uses genuinely different headers for the two credential
/// kinds: an <see cref="ApiKeyProviderCredential"/> is sent as
/// <c>x-api-key: &lt;key&gt;</c>, matching Anthropic's documented direct API
/// key authentication, while an <see cref="OAuthTokenProviderCredential"/>
/// is sent as a standard <c>Authorization: Bearer &lt;token&gt;</c> header,
/// matching Anthropic's Workload Identity Federation option. An OAuth
/// token past its <see cref="OAuthTokenProviderCredential.ExpiresAtUtc"/>
/// is rejected here, before any network call.
/// </remarks>
public static class AnthropicAuthorizationHeaderFactory
{
    private const string _apiKeyHeaderName = "x-api-key";
    private const string _bearerHeaderName = "Authorization";
    private const string _bearerScheme = "Bearer";

    /// <summary>
    /// Resolves <paramref name="credential"/> into an
    /// <see cref="AnthropicAuthorizationResult"/> for <paramref name="providerId"/>.
    /// </summary>
    /// <param name="credential">The credential to resolve.</param>
    /// <param name="providerId">The provider the request will be sent to, used for diagnostics.</param>
    /// <param name="timeProvider">The clock used to evaluate OAuth token expiry.</param>
    /// <returns>
    /// An <see cref="AnthropicAuthorizationGranted"/> carrying a usable
    /// authentication header, or an <see cref="AnthropicAuthorizationDenied"/>
    /// carrying a typed <see cref="ProviderFailureKind.Authentication"/>
    /// failure.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="credential"/> or <paramref name="timeProvider"/> is
    /// null.
    /// </exception>
    public static AnthropicAuthorizationResult Create(
        ProviderCredential credential,
        ProviderId providerId,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return credential switch
        {
            ApiKeyProviderCredential apiKey =>
                new AnthropicAuthorizationGranted(_apiKeyHeaderName, apiKey.ApiKey),

            OAuthTokenProviderCredential oauthToken => CreateFromOAuthToken(oauthToken, providerId, timeProvider),

            _ => new AnthropicAuthorizationDenied(
                new ProviderFailure(
                    ProviderFailureKind.Authentication,
                    providerId,
                    requestId: null,
                    statusCode: null,
                    providerCode: null,
                    retryAfter: null,
                    $"Credential kind '{credential.GetType().Name}' is not supported by the Anthropic integration.",
                    diagnosticCause: null,
                    ExtensionData.Empty)),
        };
    }

    private static AnthropicAuthorizationResult CreateFromOAuthToken(
        OAuthTokenProviderCredential oauthToken,
        ProviderId providerId,
        TimeProvider timeProvider)
    {
        return oauthToken.ExpiresAtUtc is { } expiresAtUtc && expiresAtUtc <= timeProvider.GetUtcNow()
            ? new AnthropicAuthorizationDenied(
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
            : new AnthropicAuthorizationGranted(_bearerHeaderName, $"{_bearerScheme} {oauthToken.AccessToken}");
    }
}
