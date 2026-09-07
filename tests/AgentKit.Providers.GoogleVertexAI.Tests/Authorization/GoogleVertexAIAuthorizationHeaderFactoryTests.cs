// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI.Tests.Authorization;

/// <summary>
/// Verifies that <see cref="GoogleVertexAIAuthorizationHeaderFactory"/>
/// sends an OAuth token as <c>Authorization: Bearer</c>, denies an API-key
/// credential outright (Vertex AI has no API-key authentication mode), and
/// rejects an expired OAuth token before any network call would occur.
/// </summary>
public sealed class GoogleVertexAIAuthorizationHeaderFactoryTests
{
    private static readonly ProviderId ProviderId = new("google-vertex-ai");
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Clock = new FakeTimeProvider(Now);

    [Fact]
    public void Create_WhenOAuthTokenNotExpired_ReturnsGrantedAuthorizationBearerHeader()
    {
        var credential = new OAuthTokenProviderCredential("access-token-123", Now.AddMinutes(5));

        var result = GoogleVertexAIAuthorizationHeaderFactory.Create(credential, ProviderId, Clock);

        var granted = result.ShouldBeOfType<GoogleVertexAIAuthorizationGranted>();
        granted.HeaderName.ShouldBe("Authorization");
        granted.HeaderValue.ShouldBe("Bearer access-token-123");
    }

    [Fact]
    public void Create_WhenOAuthTokenExpired_ReturnsDeniedAuthenticationFailure()
    {
        var credential = new OAuthTokenProviderCredential("access-token-123", Now.AddSeconds(-1));

        var result = GoogleVertexAIAuthorizationHeaderFactory.Create(credential, ProviderId, Clock);

        var denied = result.ShouldBeOfType<GoogleVertexAIAuthorizationDenied>();
        denied.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        denied.Failure.ProviderId.ShouldBe(ProviderId);
    }

    [Fact]
    public void Create_WhenOAuthTokenHasNoExpiry_ReturnsGranted()
    {
        var credential = new OAuthTokenProviderCredential("access-token-123", expiresAtUtc: null);

        var result = GoogleVertexAIAuthorizationHeaderFactory.Create(credential, ProviderId, Clock);

        _ = result.ShouldBeOfType<GoogleVertexAIAuthorizationGranted>();
    }

    [Fact]
    public void Create_WhenApiKeyCredential_ReturnsDeniedBecauseVertexHasNoApiKeyMode()
    {
        var result = GoogleVertexAIAuthorizationHeaderFactory.Create(
            new ApiKeyProviderCredential("some-key"), ProviderId, Clock);

        var denied = result.ShouldBeOfType<GoogleVertexAIAuthorizationDenied>();
        denied.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
    }
}
