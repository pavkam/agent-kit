// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests.Http;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

using AgentKit.Providers.Http;

using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies <c>Retry-After</c> resolution for delay and HTTP-date forms against an injected clock.</summary>
public sealed class RetryAfterResolverTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static HttpResponseHeaders CreateHeaders(Action<HttpResponseMessage>? configure = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        configure?.Invoke(response);
        return response.Headers;
    }

    [Fact]
    public void Resolve_WhenHeaderIsAbsent_ReturnsNull()
    {
        var headers = CreateHeaders();

        var resolved = RetryAfterResolver.Resolve(headers, new FakeTimeProvider(Now));

        resolved.ShouldBeNull();
    }

    [Fact]
    public void Resolve_WhenHeaderIsDeltaSeconds_ReturnsDelta()
    {
        var headers = CreateHeaders(response => response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30)));

        var resolved = RetryAfterResolver.Resolve(headers, new FakeTimeProvider(Now));

        resolved.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Resolve_WhenHeaderIsDeltaZero_ReturnsZero()
    {
        var headers = CreateHeaders(response => response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero));

        var resolved = RetryAfterResolver.Resolve(headers, new FakeTimeProvider(Now));

        resolved.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Resolve_WhenHeaderIsRawDeltaString_ParsesSeconds()
    {
        var headers = CreateHeaders(response => response.Headers.TryAddWithoutValidation("Retry-After", "45"));

        var resolved = RetryAfterResolver.Resolve(headers, new FakeTimeProvider(Now));

        resolved.ShouldBe(TimeSpan.FromSeconds(45));
    }

    [Fact]
    public void Resolve_WhenHeaderIsFutureHttpDate_ReturnsRemainingTimeFromInjectedClock()
    {
        var headers = CreateHeaders(response => response.Headers.RetryAfter = new RetryConditionHeaderValue(Now.AddSeconds(45)));

        var resolved = RetryAfterResolver.Resolve(headers, new FakeTimeProvider(Now));

        resolved.ShouldBe(TimeSpan.FromSeconds(45));
    }

    [Fact]
    public void Resolve_WhenHeaderIsRawHttpDateString_ParsesAgainstInjectedClock()
    {
        var headers = CreateHeaders(response => response.Headers.TryAddWithoutValidation("Retry-After", "Sun, 01 Jun 2025 12:02:00 GMT"));

        var resolved = RetryAfterResolver.Resolve(headers, new FakeTimeProvider(Now));

        resolved.ShouldBe(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void Resolve_WhenHeaderIsPastHttpDate_ClampsToZero()
    {
        var headers = CreateHeaders(response => response.Headers.RetryAfter = new RetryConditionHeaderValue(Now.AddMinutes(-5)));

        var resolved = RetryAfterResolver.Resolve(headers, new FakeTimeProvider(Now));

        resolved.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Resolve_WhenHeaderIsHttpDateEqualToNow_ReturnsZero()
    {
        var headers = CreateHeaders(response => response.Headers.RetryAfter = new RetryConditionHeaderValue(Now));

        var resolved = RetryAfterResolver.Resolve(headers, new FakeTimeProvider(Now));

        resolved.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Resolve_WhenClockAdvancesBeforeResolution_UsesCurrentInstant()
    {
        var headers = CreateHeaders(response => response.Headers.RetryAfter = new RetryConditionHeaderValue(Now.AddSeconds(60)));
        var clock = new FakeTimeProvider(Now);
        clock.Advance(TimeSpan.FromSeconds(20));

        var resolved = RetryAfterResolver.Resolve(headers, clock);

        resolved.ShouldBe(TimeSpan.FromSeconds(40));
    }

    [Fact]
    public void Resolve_WhenHeaderValueIsUnparseable_ReturnsNull()
    {
        var headers = CreateHeaders(response => response.Headers.TryAddWithoutValidation("Retry-After", "soon-ish"));

        var resolved = RetryAfterResolver.Resolve(headers, new FakeTimeProvider(Now));

        resolved.ShouldBeNull();
    }

    [Fact]
    public void Resolve_WhenHeadersIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => RetryAfterResolver.Resolve(null!, new FakeTimeProvider(Now)));

        exception.ParamName.ShouldBe("headers");
    }

    [Fact]
    public void Resolve_WhenTimeProviderIsNull_ThrowsArgumentNullException()
    {
        var headers = CreateHeaders();

        var exception = Should.Throw<ArgumentNullException>(() => RetryAfterResolver.Resolve(headers, null!));

        exception.ParamName.ShouldBe("timeProvider");
    }
}
