// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests.Http;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

using AgentKit.Providers.Http;

/// <summary>Verifies provider request-identifier header reading across single and multiple candidate names.</summary>
public sealed class ProviderRequestIdReaderTests
{
    private static HttpResponseHeaders CreateHeaders(params ReadOnlySpan<(string Name, string Value)> headers)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        foreach (var (name, value) in headers)
        {
            _ = response.Headers.TryAddWithoutValidation(name, value);
        }

        return response.Headers;
    }

    [Fact]
    public void TryRead_WhenHeaderIsPresent_ReturnsFirstValue()
    {
        var headers = CreateHeaders(("x-request-id", "req_123"));

        var requestId = ProviderRequestIdReader.TryRead(headers, "x-request-id");

        requestId.ShouldBe(new ProviderRequestId("req_123"));
    }

    [Fact]
    public void TryRead_WhenHeaderNameDiffersInCase_MatchesCaseInsensitively()
    {
        var headers = CreateHeaders(("X-Amzn-RequestId", "abc-def"));

        var requestId = ProviderRequestIdReader.TryRead(headers, "x-amzn-requestid");

        requestId.ShouldBe(new ProviderRequestId("abc-def"));
    }

    [Fact]
    public void TryRead_WhenHeaderIsAbsent_ReturnsNull()
    {
        var headers = CreateHeaders(("request-id", "other"));

        var requestId = ProviderRequestIdReader.TryRead(headers, "x-request-id");

        requestId.ShouldBeNull();
    }

    [Fact]
    public void TryRead_WhenHeaderHasMultipleValues_ReturnsFirstNonBlank()
    {
        var headers = CreateHeaders(("x-request-id", ""), ("x-request-id", "   "), ("x-request-id", "req_second"), ("x-request-id", "req_third"));

        var requestId = ProviderRequestIdReader.TryRead(headers, "x-request-id");

        requestId.ShouldBe(new ProviderRequestId("req_second"));
    }

    [Fact]
    public void TryRead_WhenAllValuesAreBlank_ReturnsNull()
    {
        var headers = CreateHeaders(("x-request-id", ""), ("x-request-id", " \t "));

        var requestId = ProviderRequestIdReader.TryRead(headers, "x-request-id");

        requestId.ShouldBeNull();
    }

    [Fact]
    public void TryRead_WhenMultipleCandidateNames_ReturnsFirstNameWithValue()
    {
        var headers = CreateHeaders(("request-id", "anthropic-id"), ("x-request-id", "generic-id"));

        var requestId = ProviderRequestIdReader.TryRead(headers, "x-request-id", "request-id");

        requestId.ShouldBe(new ProviderRequestId("generic-id"));
    }

    [Fact]
    public void TryRead_WhenFirstCandidateIsBlank_FallsThroughToNextCandidate()
    {
        var headers = CreateHeaders(("x-request-id", ""), ("request-id", "fallback-id"));

        var requestId = ProviderRequestIdReader.TryRead(headers, "x-request-id", "request-id");

        requestId.ShouldBe(new ProviderRequestId("fallback-id"));
    }

    [Fact]
    public void TryRead_WhenNoCandidateIsPresent_ReturnsNull()
    {
        var headers = CreateHeaders();

        var requestId = ProviderRequestIdReader.TryRead(headers, "x-request-id", "request-id");

        requestId.ShouldBeNull();
    }

    [Fact]
    public void TryRead_WhenHeadersIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => ProviderRequestIdReader.TryRead(null!, "x-request-id"));

        exception.ParamName.ShouldBe("headers");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void TryRead_WhenHeaderNameIsBlank_ThrowsArgumentException(string? headerName)
    {
        var headers = CreateHeaders();

        var exception = Should.Throw<ArgumentException>(() => ProviderRequestIdReader.TryRead(headers, headerName!));

        exception.ParamName.ShouldBe("headerName");
    }

    [Fact]
    public void TryRead_WhenCandidateNamesIsEmpty_ThrowsArgumentOutOfRangeException()
    {
        var headers = CreateHeaders();

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => ProviderRequestIdReader.TryRead(headers, []));

        exception.ParamName.ShouldBe("headerNames");
    }

    [Fact]
    public void TryRead_WhenACandidateNameIsBlank_ThrowsArgumentExceptionBeforeReading()
    {
        var headers = CreateHeaders(("x-request-id", "req_123"));

        var exception = Should.Throw<ArgumentException>(() => ProviderRequestIdReader.TryRead(headers, "x-request-id", " "));

        exception.ParamName.ShouldBe("headerNames");
    }
}
