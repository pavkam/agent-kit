// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Tests.Authorization;

/// <summary>
/// Verifies that <see cref="GoogleGeminiAuthorizationHeaderFactory"/> sends
/// an API key as <c>x-goog-api-key</c>, an OAuth token as
/// <c>Authorization: Bearer</c>, and rejects an expired OAuth token before
/// any network call would occur.
/// </summary>
public sealed class GoogleGeminiAuthorizationHeaderFactoryTests
{
    private static readonly ProviderId ProviderId = new("google-gemini");
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Clock = new FakeTimeProvider(Now);

    [Fact]
    public void Create_WhenApiKeyCredential_ReturnsGrantedXGoogApiKeyHeader()
    {
        var result = GoogleGeminiAuthorizationHeaderFactory.Create(
            new ApiKeyProviderCredential("AIza-test"), ProviderId, Clock);

        var granted = result.ShouldBeOfType<GoogleGeminiAuthorizationGranted>();
        granted.HeaderName.ShouldBe("x-goog-api-key");
        granted.HeaderValue.ShouldBe("AIza-test");
    }

    [Fact]
    public void Create_WhenOAuthTokenNotExpired_ReturnsGrantedAuthorizationBearerHeader()
    {
        var credential = new OAuthTokenProviderCredential("access-token-123", Now.AddMinutes(5));

        var result = GoogleGeminiAuthorizationHeaderFactory.Create(credential, ProviderId, Clock);

        var granted = result.ShouldBeOfType<GoogleGeminiAuthorizationGranted>();
        granted.HeaderName.ShouldBe("Authorization");
        granted.HeaderValue.ShouldBe("Bearer access-token-123");
    }

    [Fact]
    public void Create_WhenOAuthTokenExpired_ReturnsDeniedAuthenticationFailure()
    {
        var credential = new OAuthTokenProviderCredential("access-token-123", Now.AddSeconds(-1));

        var result = GoogleGeminiAuthorizationHeaderFactory.Create(credential, ProviderId, Clock);

        var denied = result.ShouldBeOfType<GoogleGeminiAuthorizationDenied>();
        denied.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        denied.Failure.ProviderId.ShouldBe(ProviderId);
    }

    [Fact]
    public void Create_WhenOAuthTokenHasNoExpiry_ReturnsGranted()
    {
        var credential = new OAuthTokenProviderCredential("access-token-123", expiresAtUtc: null);

        var result = GoogleGeminiAuthorizationHeaderFactory.Create(credential, ProviderId, Clock);

        _ = result.ShouldBeOfType<GoogleGeminiAuthorizationGranted>();
    }
}
