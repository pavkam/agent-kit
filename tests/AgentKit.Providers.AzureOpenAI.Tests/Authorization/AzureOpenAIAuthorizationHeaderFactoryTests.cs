// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AzureOpenAI.Tests.Authorization;

/// <summary>
/// Verifies that <see cref="AzureOpenAIAuthorizationHeaderFactory"/> sends
/// an API key as <c>api-key</c> (with no scheme prefix), an Entra token as
/// <c>Authorization: Bearer</c>, and rejects an expired Entra token before
/// any network call would occur.
/// </summary>
public sealed class AzureOpenAIAuthorizationHeaderFactoryTests
{
    private static readonly ProviderId ProviderId = new("azure-openai");
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Clock = new FakeTimeProvider(Now);

    [Fact]
    public void Create_WhenApiKeyCredential_ReturnsGrantedApiKeyHeaderWithNoSchemePrefix()
    {
        var result = AzureOpenAIAuthorizationHeaderFactory.Create(
            new ApiKeyProviderCredential("azure-resource-key"), ProviderId, Clock);

        var granted = result.ShouldBeOfType<AzureOpenAIAuthorizationGranted>();
        granted.HeaderName.ShouldBe("api-key");
        granted.HeaderValue.ShouldBe("azure-resource-key");
    }

    [Fact]
    public void Create_WhenOAuthTokenNotExpired_ReturnsGrantedAuthorizationBearerHeader()
    {
        var credential = new OAuthTokenProviderCredential("entra-token-123", Now.AddMinutes(5));

        var result = AzureOpenAIAuthorizationHeaderFactory.Create(credential, ProviderId, Clock);

        var granted = result.ShouldBeOfType<AzureOpenAIAuthorizationGranted>();
        granted.HeaderName.ShouldBe("Authorization");
        granted.HeaderValue.ShouldBe("Bearer entra-token-123");
    }

    [Fact]
    public void Create_WhenOAuthTokenExpired_ReturnsDeniedAuthenticationFailure()
    {
        var credential = new OAuthTokenProviderCredential("entra-token-123", Now.AddSeconds(-1));

        var result = AzureOpenAIAuthorizationHeaderFactory.Create(credential, ProviderId, Clock);

        var denied = result.ShouldBeOfType<AzureOpenAIAuthorizationDenied>();
        denied.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        denied.Failure.ProviderId.ShouldBe(ProviderId);
    }

    [Fact]
    public void Create_WhenOAuthTokenHasNoExpiry_ReturnsGranted()
    {
        var credential = new OAuthTokenProviderCredential("entra-token-123", expiresAtUtc: null);

        var result = AzureOpenAIAuthorizationHeaderFactory.Create(credential, ProviderId, Clock);

        _ = result.ShouldBeOfType<AzureOpenAIAuthorizationGranted>();
    }
}
