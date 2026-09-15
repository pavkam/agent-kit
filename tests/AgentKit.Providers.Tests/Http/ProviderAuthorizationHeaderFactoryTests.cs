// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests.Http;

using AgentKit.Providers.Http;

using Microsoft.Extensions.Time.Testing;

/// <summary>
/// Verifies that <see cref="ProviderAuthorizationHeaderFactory"/> maps an API
/// key through the supplied <see cref="ProviderAuthorizationScheme"/>, always
/// sends an OAuth token as <c>Authorization: Bearer</c>, rejects an expired
/// token and an unsupported credential before any network call, and never
/// places credential material in a denial message.
/// </summary>
public sealed class ProviderAuthorizationHeaderFactoryTests
{
    private static readonly ProviderId ProviderId = new("test-provider");
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Clock = new FakeTimeProvider(Now);

    [Fact]
    public void Create_WhenApiKeyAndBearerScheme_ReturnsAuthorizationBearerHeader()
    {
        var result = ProviderAuthorizationHeaderFactory.Create(
            new ApiKeyProviderCredential("sk-test-key"), ProviderId, Clock, ProviderAuthorizationScheme.BearerToken);

        var granted = result.ShouldBeOfType<ProviderAuthorizationGranted>();
        granted.HeaderName.ShouldBe("Authorization");
        granted.HeaderValue.ShouldBe("Bearer sk-test-key");
    }

    [Fact]
    public void Create_WhenApiKeyAndDedicatedHeaderScheme_ReturnsBareKeyInThatHeader()
    {
        var scheme = ProviderAuthorizationScheme.ForApiKeyHeader("x-api-key");

        var result = ProviderAuthorizationHeaderFactory.Create(
            new ApiKeyProviderCredential("sk-ant-test"), ProviderId, Clock, scheme);

        var granted = result.ShouldBeOfType<ProviderAuthorizationGranted>();
        granted.HeaderName.ShouldBe("x-api-key");
        granted.HeaderValue.ShouldBe("sk-ant-test");
    }

    [Fact]
    public void Create_WhenApiKeyAndCustomPrefix_ConcatenatesPrefixAndKey()
    {
        var scheme = new ProviderAuthorizationScheme("Authorization", "Token ");

        var result = ProviderAuthorizationHeaderFactory.Create(
            new ApiKeyProviderCredential("abc"), ProviderId, Clock, scheme);

        result.ShouldBeOfType<ProviderAuthorizationGranted>().HeaderValue.ShouldBe("Token abc");
    }

    [Fact]
    public void Create_WhenApiKeyAndOAuthTokenOnlyScheme_ReturnsDeniedAuthenticationFailure()
    {
        var result = ProviderAuthorizationHeaderFactory.Create(
            new ApiKeyProviderCredential("some-key"), ProviderId, Clock, ProviderAuthorizationScheme.OAuthTokenOnly);

        var denied = result.ShouldBeOfType<ProviderAuthorizationDenied>();
        denied.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        denied.Failure.ProviderId.ShouldBe(ProviderId);
        denied.Failure.SafeMessage.ShouldContain(nameof(ApiKeyProviderCredential));
        denied.Failure.SafeMessage.ShouldContain("test-provider");
        denied.Failure.SafeMessage.ShouldContain("no API-key authentication mode");
        denied.Failure.SafeMessage.ShouldNotContain("some-key");
    }

    [Fact]
    public void Create_WhenOAuthTokenNotExpired_ReturnsAuthorizationBearerHeaderRegardlessOfScheme()
    {
        var credential = new OAuthTokenProviderCredential("access-token-123", Now.AddMinutes(5));

        var result = ProviderAuthorizationHeaderFactory.Create(
            credential, ProviderId, Clock, ProviderAuthorizationScheme.ForApiKeyHeader("x-goog-api-key"));

        var granted = result.ShouldBeOfType<ProviderAuthorizationGranted>();
        granted.HeaderName.ShouldBe("Authorization");
        granted.HeaderValue.ShouldBe("Bearer access-token-123");
    }

    [Fact]
    public void Create_WhenOAuthTokenAndOAuthTokenOnlyScheme_ReturnsGranted()
    {
        var credential = new OAuthTokenProviderCredential("gcp-token", Now.AddMinutes(5));

        var result = ProviderAuthorizationHeaderFactory.Create(
            credential, ProviderId, Clock, ProviderAuthorizationScheme.OAuthTokenOnly);

        result.ShouldBeOfType<ProviderAuthorizationGranted>().HeaderValue.ShouldBe("Bearer gcp-token");
    }

    [Fact]
    public void Create_WhenOAuthTokenHasNoExpiry_ReturnsGranted()
    {
        var credential = new OAuthTokenProviderCredential("access-token-123", expiresAtUtc: null);

        var result = ProviderAuthorizationHeaderFactory.Create(
            credential, ProviderId, Clock, ProviderAuthorizationScheme.BearerToken);

        _ = result.ShouldBeOfType<ProviderAuthorizationGranted>();
    }

    [Fact]
    public void Create_WhenOAuthTokenExpired_ReturnsDeniedAuthenticationFailureWithoutToken()
    {
        var credential = new OAuthTokenProviderCredential("access-token-123", Now.AddSeconds(-1));

        var result = ProviderAuthorizationHeaderFactory.Create(
            credential, ProviderId, Clock, ProviderAuthorizationScheme.BearerToken);

        var denied = result.ShouldBeOfType<ProviderAuthorizationDenied>();
        denied.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        denied.Failure.ProviderId.ShouldBe(ProviderId);
        denied.Failure.SafeMessage.ShouldBe("The configured OAuth access token has expired.");
        denied.Failure.SafeMessage.ShouldNotContain("access-token-123");
    }

    [Fact]
    public void Create_WhenOAuthTokenExpiresExactlyNow_ReturnsDenied()
    {
        var credential = new OAuthTokenProviderCredential("access-token-123", Now);

        var result = ProviderAuthorizationHeaderFactory.Create(
            credential, ProviderId, Clock, ProviderAuthorizationScheme.BearerToken);

        _ = result.ShouldBeOfType<ProviderAuthorizationDenied>();
    }

    [Fact]
    public void Create_WhenOAuthTokenExpiryIsEvaluated_UsesInjectedClockNotAmbientTime()
    {
        var farFuture = new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(farFuture.AddSeconds(1));
        var credential = new OAuthTokenProviderCredential("access-token-123", farFuture);

        var result = ProviderAuthorizationHeaderFactory.Create(
            credential, ProviderId, clock, ProviderAuthorizationScheme.BearerToken);

        _ = result.ShouldBeOfType<ProviderAuthorizationDenied>();
    }

    [Fact]
    public void Create_WhenCredentialIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => ProviderAuthorizationHeaderFactory.Create(
            null!, ProviderId, Clock, ProviderAuthorizationScheme.BearerToken));

        exception.ParamName.ShouldBe("credential");
    }

    [Fact]
    public void Create_WhenTimeProviderIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => ProviderAuthorizationHeaderFactory.Create(
            new ApiKeyProviderCredential("k"), ProviderId, null!, ProviderAuthorizationScheme.BearerToken));

        exception.ParamName.ShouldBe("timeProvider");
    }

    [Fact]
    public void Create_WhenSchemeIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => ProviderAuthorizationHeaderFactory.Create(
            new ApiKeyProviderCredential("k"), ProviderId, Clock, null!));

        exception.ParamName.ShouldBe("scheme");
    }
}
