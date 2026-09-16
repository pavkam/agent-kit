// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests.Http;

using AgentKit.Providers.Http;

/// <summary>
/// Verifies the argument constraints and preset values of
/// <see cref="ProviderAuthorizationScheme"/>.
/// </summary>
public sealed class ProviderAuthorizationSchemeTests
{
    [Fact]
    public void BearerToken_WhenRead_UsesAuthorizationHeaderWithBearerPrefix()
    {
        var scheme = ProviderAuthorizationScheme.BearerToken;

        scheme.ApiKeyHeaderName.ShouldBe("Authorization");
        scheme.ApiKeyValuePrefix.ShouldBe("Bearer ");
        scheme.SupportsApiKey.ShouldBeTrue();
    }

    [Fact]
    public void OAuthTokenOnly_WhenRead_HasNoApiKeyHeader()
    {
        var scheme = ProviderAuthorizationScheme.OAuthTokenOnly;

        scheme.ApiKeyHeaderName.ShouldBeNull();
        scheme.ApiKeyValuePrefix.ShouldBe(string.Empty);
        scheme.SupportsApiKey.ShouldBeFalse();
    }

    [Fact]
    public void ForApiKeyHeader_WhenGivenName_UsesBareKeyWithoutPrefix()
    {
        var scheme = ProviderAuthorizationScheme.ForApiKeyHeader("x-api-key");

        scheme.ApiKeyHeaderName.ShouldBe("x-api-key");
        scheme.ApiKeyValuePrefix.ShouldBe(string.Empty);
        scheme.SupportsApiKey.ShouldBeTrue();
    }

    [Fact]
    public void ForApiKeyHeader_WhenNameIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => ProviderAuthorizationScheme.ForApiKeyHeader(null!));

        exception.ParamName.ShouldBe("headerName");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ForApiKeyHeader_WhenNameIsBlank_ThrowsArgumentException(string headerName)
    {
        var exception = Should.Throw<ArgumentException>(() => ProviderAuthorizationScheme.ForApiKeyHeader(headerName));

        exception.ParamName.ShouldBe("headerName");
    }

    [Fact]
    public void Constructor_WhenHeaderNameIsNull_AllowsOAuthOnlyScheme()
    {
        var scheme = new ProviderAuthorizationScheme(apiKeyHeaderName: null, string.Empty);

        scheme.SupportsApiKey.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WhenHeaderNameIsBlank_ThrowsArgumentException(string headerName)
    {
        var exception = Should.Throw<ArgumentException>(() => new ProviderAuthorizationScheme(headerName, string.Empty));

        exception.ParamName.ShouldBe("apiKeyHeaderName");
    }

    [Fact]
    public void Constructor_WhenPrefixIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ProviderAuthorizationScheme("Authorization", null!));

        exception.ParamName.ShouldBe("apiKeyValuePrefix");
    }

    [Fact]
    public void Equals_WhenSameShape_ReturnsTrue()
    {
        new ProviderAuthorizationScheme("Authorization", "Bearer ").ShouldBe(ProviderAuthorizationScheme.BearerToken);
        ProviderAuthorizationScheme.ForApiKeyHeader("api-key").ShouldNotBe(ProviderAuthorizationScheme.BearerToken);
    }

    [Fact]
    public void WithExpression_WhenReplacingPrefix_ProducesIndependentCopy()
    {
        var original = ProviderAuthorizationScheme.ForApiKeyHeader("api-key");

        var copy = original with { ApiKeyValuePrefix = "Bearer " };

        copy.ApiKeyValuePrefix.ShouldBe("Bearer ");
        original.ApiKeyValuePrefix.ShouldBe(string.Empty);
        copy.ShouldNotBe(original);
    }
}
