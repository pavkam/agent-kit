// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests.Authorization;
/// <summary>
/// Verifies that <see cref="OpenAIAuthorizationHeaderFactory"/> correctly
/// distinguishes API key and OAuth token credentials, and rejects an
/// expired OAuth token before any network call would occur.
/// </summary>
public sealed class OpenAIAuthorizationHeaderFactoryTests
{
    private static readonly ProviderId ProviderId = new("openai");
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Clock = new FakeTimeProvider(Now);

    [Fact]
    public void Create_WhenApiKeyCredential_ReturnsGrantedBearerHeader()
    {
        var result = OpenAIAuthorizationHeaderFactory.Create(
            new ApiKeyProviderCredential("sk-test-key"), ProviderId, Clock);

        var granted = result.ShouldBeOfType<OpenAIAuthorizationGranted>();
        granted.Authorization.Scheme.ShouldBe("Bearer");
        granted.Authorization.Parameter.ShouldBe("sk-test-key");
    }

    [Fact]
    public void Create_WhenOAuthTokenNotExpired_ReturnsGrantedBearerHeader()
    {
        var credential = new OAuthTokenProviderCredential("access-token-123", Now.AddMinutes(5));

        var result = OpenAIAuthorizationHeaderFactory.Create(credential, ProviderId, Clock);

        var granted = result.ShouldBeOfType<OpenAIAuthorizationGranted>();
        granted.Authorization.Scheme.ShouldBe("Bearer");
        granted.Authorization.Parameter.ShouldBe("access-token-123");
    }

    [Fact]
    public void Create_WhenOAuthTokenHasNoExpiry_ReturnsGrantedBearerHeader()
    {
        var credential = new OAuthTokenProviderCredential("access-token-123", expiresAtUtc: null);

        var result = OpenAIAuthorizationHeaderFactory.Create(credential, ProviderId, Clock);

        _ = result.ShouldBeOfType<OpenAIAuthorizationGranted>();
    }

    [Fact]
    public void Create_WhenOAuthTokenExpired_ReturnsDeniedAuthenticationFailure()
    {
        var credential = new OAuthTokenProviderCredential("access-token-123", Now.AddSeconds(-1));

        var result = OpenAIAuthorizationHeaderFactory.Create(credential, ProviderId, Clock);

        var denied = result.ShouldBeOfType<OpenAIAuthorizationDenied>();
        denied.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        denied.Failure.ProviderId.ShouldBe(ProviderId);
    }

    [Fact]
    public void Create_WhenOAuthTokenExpiresExactlyNow_ReturnsDeniedAuthenticationFailure()
    {
        var credential = new OAuthTokenProviderCredential("access-token-123", Now);

        var result = OpenAIAuthorizationHeaderFactory.Create(credential, ProviderId, Clock);

        _ = result.ShouldBeOfType<OpenAIAuthorizationDenied>();
    }

}
