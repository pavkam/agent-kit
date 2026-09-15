// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests.Http;

using System.Net.Http;

using AgentKit.Providers.Http;

/// <summary>
/// Verifies that <see cref="ProviderAuthorizationGranted"/> guards its
/// arguments, applies the header to an outgoing request the way each
/// adapter previously did by hand, and never renders the secret header
/// value through <see cref="object.ToString"/> or record member printing.
/// </summary>
public sealed class ProviderAuthorizationGrantedTests
{
    private const string Secret = "sk-live-0123456789abcdef";

    /// <summary>
    /// Renders the record through string interpolation, the path a message
    /// template or exception text would take. A sealed record cannot be
    /// derived from, so its <c>PrintMembers</c> override is observed through
    /// the <see cref="object.ToString"/> output it feeds.
    /// </summary>
    private static string RenderViaInterpolation(ProviderAuthorizationGranted granted) =>
        FormattableString.Invariant($"granted={granted}");

    [Fact]
    public void Constructor_WhenHeaderNameIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ProviderAuthorizationGranted(null!, Secret));

        exception.ParamName.ShouldBe("headerName");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenHeaderNameIsBlank_ThrowsArgumentException(string headerName)
    {
        var exception = Should.Throw<ArgumentException>(() => new ProviderAuthorizationGranted(headerName, Secret));

        exception.ParamName.ShouldBe("headerName");
    }

    [Fact]
    public void Constructor_WhenHeaderValueIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ProviderAuthorizationGranted("x-api-key", null!));

        exception.ParamName.ShouldBe("headerValue");
    }

    [Theory]
    [InlineData("")]
    [InlineData("\t")]
    public void Constructor_WhenHeaderValueIsBlank_ThrowsArgumentException(string headerValue)
    {
        var exception = Should.Throw<ArgumentException>(() => new ProviderAuthorizationGranted("x-api-key", headerValue));

        exception.ParamName.ShouldBe("headerValue");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesBothProperties()
    {
        var granted = new ProviderAuthorizationGranted("x-api-key", Secret);

        granted.HeaderName.ShouldBe("x-api-key");
        granted.HeaderValue.ShouldBe(Secret);
    }

    [Fact]
    public void ToString_WhenCalled_DoesNotContainHeaderValue()
    {
        var granted = new ProviderAuthorizationGranted("Authorization", "Bearer " + Secret);

        var text = granted.ToString();

        text.ShouldNotContain(Secret);
        text.ShouldNotContain("Bearer " + Secret);
        text.ShouldBe("ProviderAuthorizationGranted { HeaderName = Authorization, HeaderValue = [REDACTED] }");
    }

    [Fact]
    public void ToString_WhenInterpolated_StillRedactsHeaderValue()
    {
        var granted = new ProviderAuthorizationGranted("x-goog-api-key", Secret);

        var text = RenderViaInterpolation(granted);

        text.ShouldNotContain(Secret);
        text.ShouldContain("HeaderName = x-goog-api-key");
        text.ShouldContain("HeaderValue = " + ProviderAuthorizationGranted.RedactionMarker);
    }

    [Fact]
    public void ToString_WhenCopiedWithInit_RedactsTheNewValueToo()
    {
        var granted = new ProviderAuthorizationGranted("api-key", "first") with { HeaderValue = Secret };

        granted.ToString().ShouldNotContain(Secret);
        granted.HeaderValue.ShouldBe(Secret);
    }

    [Fact]
    public void Equals_WhenHeaderValuesDiffer_ReturnsFalse()
    {
        var first = new ProviderAuthorizationGranted("x-api-key", "a");
        var second = new ProviderAuthorizationGranted("x-api-key", "b");

        first.ShouldNotBe(second);
        new ProviderAuthorizationGranted("x-api-key", "a").ShouldBe(first);
    }

    [Fact]
    public void Apply_WhenHeadersIsNull_ThrowsArgumentNullException()
    {
        var granted = new ProviderAuthorizationGranted("x-api-key", Secret);

        var exception = Should.Throw<ArgumentNullException>(() => granted.Apply(null!));

        exception.ParamName.ShouldBe("headers");
    }

    [Fact]
    public void Apply_WhenHeaderIsAuthorizationBearer_SetsTypedAuthorizationProperty()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.test/v1/chat");
        var granted = new ProviderAuthorizationGranted("Authorization", "Bearer " + Secret);

        granted.Apply(request.Headers);

        var authorization = request.Headers.Authorization.ShouldNotBeNull();
        authorization.Scheme.ShouldBe("Bearer");
        authorization.Parameter.ShouldBe(Secret);
        request.Headers.GetValues("Authorization").ShouldHaveSingleItem().ShouldBe("Bearer " + Secret);
    }

    [Fact]
    public void Apply_WhenHeaderIsAuthorizationInDifferentCase_StillSetsTypedAuthorizationProperty()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.test/");
        var granted = new ProviderAuthorizationGranted("authorization", "Bearer " + Secret);

        granted.Apply(request.Headers);

        request.Headers.Authorization.ShouldNotBeNull().Parameter.ShouldBe(Secret);
    }

    [Fact]
    public void Apply_WhenHeaderIsDedicatedApiKeyHeader_AddsRawHeaderAndLeavesAuthorizationUnset()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.test/v1/messages");
        var granted = new ProviderAuthorizationGranted("x-api-key", Secret);

        granted.Apply(request.Headers);

        request.Headers.GetValues("x-api-key").ShouldHaveSingleItem().ShouldBe(Secret);
        request.Headers.Authorization.ShouldBeNull();
        request.Headers.Contains("Authorization").ShouldBeFalse();
    }

    [Fact]
    public void Apply_WhenHeaderIsAzureApiKey_AddsRawHeaderWithoutSchemePrefix()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.test/");
        var granted = new ProviderAuthorizationGranted("api-key", Secret);

        granted.Apply(request.Headers);

        request.Headers.GetValues("api-key").ShouldHaveSingleItem().ShouldBe(Secret);
    }

    [Fact]
    public void Apply_WhenAuthorizationValueHasNoScheme_FallsBackToRawHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.test/");
        var granted = new ProviderAuthorizationGranted("Authorization", "=malformed");

        granted.Apply(request.Headers);

        request.Headers.GetValues("Authorization").ShouldHaveSingleItem().ShouldBe("=malformed");
    }
}
