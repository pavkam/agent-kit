// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic.Tests.Authorization;

/// <summary>
/// Verifies that <see cref="AnthropicAuthorizationHeaderFactory"/> sends an
/// API key as <c>x-api-key</c>, an OAuth token as
/// <c>Authorization: Bearer</c>, and rejects an expired OAuth token before
/// any network call would occur.
/// </summary>
public sealed class AnthropicAuthorizationHeaderFactoryTests
{
    private static readonly ProviderId ProviderId = new("anthropic");
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Clock = new FakeTimeProvider(Now);

    [Fact]
    public void Create_WhenApiKeyCredential_ReturnsGrantedXApiKeyHeader()
    {
        var result = AnthropicAuthorizationHeaderFactory.Create(
            new ApiKeyProviderCredential("sk-ant-test"), ProviderId, Clock);

        var granted = result.ShouldBeOfType<AnthropicAuthorizationGranted>();
        granted.HeaderName.ShouldBe("x-api-key");
        granted.HeaderValue.ShouldBe("sk-ant-test");
    }

    [Fact]
    public void Create_WhenOAuthTokenNotExpired_ReturnsGrantedAuthorizationBearerHeader()
    {
        var credential = new OAuthTokenProviderCredential("access-token-123", Now.AddMinutes(5));

        var result = AnthropicAuthorizationHeaderFactory.Create(credential, ProviderId, Clock);

        var granted = result.ShouldBeOfType<AnthropicAuthorizationGranted>();
        granted.HeaderName.ShouldBe("Authorization");
        granted.HeaderValue.ShouldBe("Bearer access-token-123");
    }

    [Fact]
    public void Create_WhenOAuthTokenExpired_ReturnsDeniedAuthenticationFailure()
    {
        var credential = new OAuthTokenProviderCredential("access-token-123", Now.AddSeconds(-1));

        var result = AnthropicAuthorizationHeaderFactory.Create(credential, ProviderId, Clock);

        var denied = result.ShouldBeOfType<AnthropicAuthorizationDenied>();
        denied.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        denied.Failure.ProviderId.ShouldBe(ProviderId);
    }

    [Fact]
    public void Create_WhenOAuthTokenHasNoExpiry_ReturnsGranted()
    {
        var credential = new OAuthTokenProviderCredential("access-token-123", expiresAtUtc: null);

        var result = AnthropicAuthorizationHeaderFactory.Create(credential, ProviderId, Clock);

        _ = result.ShouldBeOfType<AnthropicAuthorizationGranted>();
    }
}
