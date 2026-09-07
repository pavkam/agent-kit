// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AzureOpenAI;

/// <summary>
/// Resolves a provider-neutral <see cref="ProviderCredential"/> into the
/// concrete HTTP authentication header the Azure OpenAI GA v1 API expects.
/// </summary>
/// <remarks>
/// Azure OpenAI documents two authentication mechanisms:
/// <c>api-key: AZURE_OPENAI_API_KEY</c>, and
/// <c>Authorization: Bearer &lt;Microsoft Entra token&gt;</c> acquired for
/// the <c>https://cognitiveservices.azure.com/.default</c> scope. An
/// <see cref="ApiKeyProviderCredential"/> is sent as the former, with no
/// <c>Bearer</c> scheme prefix; an <see cref="OAuthTokenProviderCredential"/>
/// is sent as the latter. An Entra token past its
/// <see cref="OAuthTokenProviderCredential.ExpiresAtUtc"/> is rejected
/// here, before any network call.
/// </remarks>
public static class AzureOpenAIAuthorizationHeaderFactory
{
    private const string _apiKeyHeaderName = "api-key";
    private const string _bearerHeaderName = "Authorization";
    private const string _bearerScheme = "Bearer";

    /// <summary>
    /// Resolves <paramref name="credential"/> into an
    /// <see cref="AzureOpenAIAuthorizationResult"/> for <paramref name="providerId"/>.
    /// </summary>
    /// <param name="credential">The credential to resolve.</param>
    /// <param name="providerId">The provider the request will be sent to, used for diagnostics.</param>
    /// <param name="timeProvider">The clock used to evaluate Entra token expiry.</param>
    /// <returns>
    /// An <see cref="AzureOpenAIAuthorizationGranted"/> carrying a usable
    /// authentication header, or an <see cref="AzureOpenAIAuthorizationDenied"/>
    /// carrying a typed <see cref="ProviderFailureKind.Authentication"/>
    /// failure.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="credential"/> or <paramref name="timeProvider"/> is
    /// null.
    /// </exception>
    public static AzureOpenAIAuthorizationResult Create(
        ProviderCredential credential,
        ProviderId providerId,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return credential switch
        {
            ApiKeyProviderCredential apiKey =>
                new AzureOpenAIAuthorizationGranted(_apiKeyHeaderName, apiKey.ApiKey),

            OAuthTokenProviderCredential oauthToken => CreateFromOAuthToken(oauthToken, providerId, timeProvider),

            _ => new AzureOpenAIAuthorizationDenied(
                new ProviderFailure(
                    ProviderFailureKind.Authentication,
                    providerId,
                    requestId: null,
                    statusCode: null,
                    providerCode: null,
                    retryAfter: null,
                    $"Credential kind '{credential.GetType().Name}' is not supported by the Azure OpenAI integration.",
                    diagnosticCause: null,
                    ExtensionData.Empty)),
        };
    }

    private static AzureOpenAIAuthorizationResult CreateFromOAuthToken(
        OAuthTokenProviderCredential oauthToken,
        ProviderId providerId,
        TimeProvider timeProvider)
    {
        return oauthToken.ExpiresAtUtc is { } expiresAtUtc && expiresAtUtc <= timeProvider.GetUtcNow()
            ? new AzureOpenAIAuthorizationDenied(
                new ProviderFailure(
                    ProviderFailureKind.Authentication,
                    providerId,
                    requestId: null,
                    statusCode: null,
                    providerCode: null,
                    retryAfter: null,
                    "The configured Microsoft Entra access token has expired.",
                    diagnosticCause: null,
                    ExtensionData.Empty))
            : new AzureOpenAIAuthorizationGranted(_bearerHeaderName, $"{_bearerScheme} {oauthToken.AccessToken}");
    }
}
