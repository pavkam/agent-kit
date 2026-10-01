// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests.Egress;

using System.Net.Http;
using System.Text;

using AgentKit.Providers.Egress;

using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies that <see cref="ProviderRequestAuthenticationTarget"/> exposes the frozen request and receives headers.</summary>
public sealed class ProviderRequestAuthenticationTargetTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static ProviderRequestAuthenticationTarget Create(
        HttpRequestMessage message,
        NetworkRequestContent? content = null,
        TimeProvider? timeProvider = null) =>
        new(message, new ProviderId("openai"), ProviderAuthorizationScheme.BearerToken, content, timeProvider ?? new FakeTimeProvider(Now));

    [Fact]
    public void Properties_WhenCreatedOverAFrozenBody_DescribeTheExactRequest()
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.test/v1/chat?x=1");
        var content = new NetworkRequestContent("application/json", Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"a\":1}"));

        var target = Create(message, content);

        target.ProviderId.ShouldBe(new ProviderId("openai"));
        target.Scheme.ShouldBe(ProviderAuthorizationScheme.BearerToken);
        target.Method.ShouldBe("POST");
        target.RequestUri.ShouldBe(new Uri("https://api.test/v1/chat?x=1"));
        target.ContentType.ShouldBe("application/json");
        Encoding.UTF8.GetString(target.Body.Span).ShouldBe(/*lang=json,strict*/ "{\"a\":1}");
    }

    [Fact]
    public void Body_WhenRequestHasNoContent_IsEmptyWithNoContentType()
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, "https://api.test/v1/models");

        var target = Create(message);

        target.Body.IsEmpty.ShouldBeTrue();
        target.ContentType.ShouldBeNull();
        target.Method.ShouldBe("GET");
    }

    [Fact]
    public void UtcNow_WhenClockAdvances_ReportsTheInjectedClock()
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.test/");
        var clock = new FakeTimeProvider(Now);
        var target = Create(message, timeProvider: clock);

        target.UtcNow.ShouldBe(Now);
        clock.Advance(TimeSpan.FromMinutes(5));
        target.UtcNow.ShouldBe(Now.AddMinutes(5));
    }

    [Fact]
    public void SetHeader_WhenBearerAuthorization_SetsTheTypedAuthorizationProperty()
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.test/");
        var target = Create(message);

        target.SetHeader("Authorization", "Bearer token-value");

        message.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        message.Headers.Authorization.Parameter.ShouldBe("token-value");
    }

    [Fact]
    public void SetHeader_WhenHeaderAlreadyPresent_ReplacesItInsteadOfAppending()
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.test/");
        _ = message.Headers.TryAddWithoutValidation("x-api-key", "stale");
        var target = Create(message);

        target.SetHeader("x-api-key", "fresh");

        message.Headers.GetValues("x-api-key").ShouldBe(["fresh"]);
    }

    [Fact]
    public void SetHeader_WhenNameOrValueIsBlank_ThrowsArgumentException()
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.test/");
        var target = Create(message);

        Should.Throw<ArgumentException>(() => target.SetHeader(" ", "v")).ParamName.ShouldBe("name");
        Should.Throw<ArgumentException>(() => target.SetHeader("h", "")).ParamName.ShouldBe("value");
        message.Headers.ShouldBeEmpty();
    }
}
