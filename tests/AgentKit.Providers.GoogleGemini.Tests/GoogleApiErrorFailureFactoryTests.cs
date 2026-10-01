// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Tests;

using System.Net;
using System.Net.Http.Headers;

using AgentKit.Providers.Egress;
using AgentKit.Providers.Http;
using AgentKit.TestSupport;

/// <summary>Verifies <see cref="GoogleApiErrorFailureFactory"/> behavior and contracts.</summary>
public sealed class GoogleApiErrorFailureFactoryTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly ProviderId Provider = new("google-test");

    private static ProviderEgressResponse Response(HttpStatusCode statusCode, string? body = null, string mediaType = "application/json") =>
        new(new StreamNetworkResponse(
            (int) statusCode,
            new MemoryStream(body is null ? [] : Encoding.UTF8.GetBytes(body)),
            body is null ? [] : [new NetworkHeader("Content-Type", mediaType)]));

    private static ProviderEgressResponse FaultingResponse(HttpStatusCode statusCode, FaultingReadStream stream) =>
        new(new StreamNetworkResponse((int) statusCode, stream));

    [Fact]
    public async Task CreateAsync_WhenBodyCarriesMappedStatus_UsesBodyStatusAndRetainsCodeAndMessageEvidence()
    {
        await using var response = Response(
            HttpStatusCode.BadRequest,
            /*lang=json,strict*/ """{ "error": { "code": 400, "message": "Invalid value at 'contents'.", "status": "INVALID_ARGUMENT" } }""");

        var failure = await GoogleApiErrorFailureFactory.CreateAsync(response, Provider, new FakeTimeProvider(Now), TestContext.Current.CancellationToken);

        failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        failure.ProviderId.ShouldBe(Provider);
        failure.StatusCode.ShouldBe(400);
        failure.ProviderCode.ShouldBe("INVALID_ARGUMENT");
        failure.SafeMessage.ShouldBe("The provider returned HTTP status 400.");
        failure.DiagnosticCause.ShouldBeNull();
        ProviderErrorMessageEvidence.TryRead(failure.Extensions).ShouldBe("Invalid value at 'contents'.");
    }

    /// <summary>Verifies the body status wins over the HTTP status when both are present but disagree.</summary>
    [Fact]
    public async Task CreateAsync_WhenBodyStatusAndHttpStatusDisagree_PrefersBodyStatus()
    {
        await using var response = Response(
            HttpStatusCode.InternalServerError,
            /*lang=json,strict*/ """{ "error": { "code": 500, "message": "Quota exceeded.", "status": "RESOURCE_EXHAUSTED" } }""");

        var failure = await GoogleApiErrorFailureFactory.CreateAsync(response, Provider, new FakeTimeProvider(Now), TestContext.Current.CancellationToken);

        failure.Kind.ShouldBe(ProviderFailureKind.Throttling);
        failure.StatusCode.ShouldBe(500);
        failure.ProviderCode.ShouldBe("RESOURCE_EXHAUSTED");
    }

    /// <summary>Verifies an unmapped canonical status falls back to the HTTP status table while the raw code is kept.</summary>
    [Theory]
    [InlineData(HttpStatusCode.BadRequest, ProviderFailureKind.InvalidRequest)]
    [InlineData(HttpStatusCode.TooManyRequests, ProviderFailureKind.Throttling)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ProviderFailureKind.Unavailable)]
    public async Task CreateAsync_WhenBodyStatusIsUnmapped_FallsBackToHttpStatusAndKeepsProviderCode(HttpStatusCode statusCode, ProviderFailureKind expected)
    {
        await using var response = Response(
            statusCode,
            /*lang=json,strict*/ """{ "error": { "code": 0, "message": "future", "status": "FUTURE_STATUS" } }""");

        var failure = await GoogleApiErrorFailureFactory.CreateAsync(response, Provider, new FakeTimeProvider(Now), TestContext.Current.CancellationToken);

        failure.Kind.ShouldBe(expected);
        failure.ProviderCode.ShouldBe("FUTURE_STATUS");
        failure.StatusCode.ShouldBe((int) statusCode);
    }

    [Fact]
    public async Task CreateAsync_WhenBodyIsNotJson_FallsBackToHttpStatusWithParseCauseAndNoEvidence()
    {
        await using var response = Response(HttpStatusCode.BadGateway, "<html>bad gateway</html>", "text/html");

        var failure = await GoogleApiErrorFailureFactory.CreateAsync(response, Provider, new FakeTimeProvider(Now), TestContext.Current.CancellationToken);

        failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failure.StatusCode.ShouldBe(502);
        failure.ProviderCode.ShouldBeNull();
        failure.SafeMessage.ShouldBe("The provider returned HTTP status 502.");
        _ = failure.DiagnosticCause.ShouldBeOfType<JsonException>();
        ProviderErrorMessageEvidence.TryRead(failure.Extensions).ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_WhenBodyIsAbsent_FallsBackToHttpStatus()
    {
        await using var response = Response(HttpStatusCode.Conflict);

        var failure = await GoogleApiErrorFailureFactory.CreateAsync(response, Provider, new FakeTimeProvider(Now), TestContext.Current.CancellationToken);

        failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        failure.StatusCode.ShouldBe(409);
        failure.ProviderCode.ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_WhenBodyReadFaults_KeepsHttpStatusAndRetainsCause()
    {
        await using var response = FaultingResponse(
            HttpStatusCode.InternalServerError,
            FaultingReadStream.ConnectionReset("{\"error\":"u8.ToArray()));

        var failure = await GoogleApiErrorFailureFactory.CreateAsync(response, Provider, new FakeTimeProvider(Now), TestContext.Current.CancellationToken);

        failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failure.StatusCode.ShouldBe(500);
        _ = failure.DiagnosticCause.ShouldBeOfType<IOException>();
    }

    [Fact]
    public async Task CreateAsync_WhenRetryAfterHeaderPresent_ResolvesRetryAfter()
    {
        await using var response = Response(
            HttpStatusCode.TooManyRequests,
            /*lang=json,strict*/ """{ "error": { "code": 429, "message": "slow down", "status": "RESOURCE_EXHAUSTED" } }""");
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(7));

        var failure = await GoogleApiErrorFailureFactory.CreateAsync(response, Provider, new FakeTimeProvider(Now), TestContext.Current.CancellationToken);

        failure.Kind.ShouldBe(ProviderFailureKind.Throttling);
        failure.RetryAfter.ShouldBe(TimeSpan.FromSeconds(7));
    }

    [Fact]
    public async Task CreateAsync_WhenBodyCarriesRetryInfoDetailAndNoHeader_ResolvesRetryAfterFromRetryDelay()
    {
        // Google APIs communicate RESOURCE_EXHAUSTED retry guidance through error.details[] entries of
        // type google.rpc.RetryInfo with a retryDelay (such as "20s"), not through a Retry-After header.
        await using var response = Response(
            HttpStatusCode.TooManyRequests,
            /*lang=json,strict*/ """
            {
              "error": {
                "code": 429,
                "message": "Resource has been exhausted.",
                "status": "RESOURCE_EXHAUSTED",
                "details": [
                  { "@type": "type.googleapis.com/google.rpc.RetryInfo", "retryDelay": "20s" }
                ]
              }
            }
            """);

        var failure = await GoogleApiErrorFailureFactory.CreateAsync(response, Provider, new FakeTimeProvider(Now), TestContext.Current.CancellationToken);

        failure.Kind.ShouldBe(ProviderFailureKind.Throttling);
        failure.RetryAfter.ShouldBe(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public async Task CreateAsync_WhenBodyCarriesFractionalRetryDelayAmongOtherDetailTypes_ParsesTheRetryInfoEntry()
    {
        await using var response = Response(
            HttpStatusCode.TooManyRequests,
            /*lang=json,strict*/ """
            {
              "error": {
                "code": 429,
                "message": "Resource has been exhausted.",
                "status": "RESOURCE_EXHAUSTED",
                "details": [
                  { "@type": "type.googleapis.com/google.rpc.ErrorInfo", "reason": "RATE_LIMIT_EXCEEDED" },
                  { "@type": "type.googleapis.com/google.rpc.RetryInfo", "retryDelay": "1.500s" }
                ]
              }
            }
            """);

        var failure = await GoogleApiErrorFailureFactory.CreateAsync(response, Provider, new FakeTimeProvider(Now), TestContext.Current.CancellationToken);

        failure.RetryAfter.ShouldBe(TimeSpan.FromSeconds(1.5));
    }

    [Fact]
    public async Task CreateAsync_WhenBothHeaderAndRetryInfoDetailPresent_PrefersTheHeader()
    {
        await using var response = Response(
            HttpStatusCode.TooManyRequests,
            /*lang=json,strict*/ """
            {
              "error": {
                "code": 429,
                "message": "slow down",
                "status": "RESOURCE_EXHAUSTED",
                "details": [
                  { "@type": "type.googleapis.com/google.rpc.RetryInfo", "retryDelay": "20s" }
                ]
              }
            }
            """);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(7));

        var failure = await GoogleApiErrorFailureFactory.CreateAsync(response, Provider, new FakeTimeProvider(Now), TestContext.Current.CancellationToken);

        failure.RetryAfter.ShouldBe(TimeSpan.FromSeconds(7));
    }

    [Fact]
    public async Task CreateAsync_WhenHostileMessage_NeverPlacesItInSafeMessage()
    {
        const string hostile = "Authorization failed for sk-live-super-secret; tenant alice@example.test.";
        await using var response = Response(
            HttpStatusCode.Unauthorized,
            JsonSerializer.Serialize(new { error = new { code = 401, message = hostile, status = "UNAUTHENTICATED" } }));

        var failure = await GoogleApiErrorFailureFactory.CreateAsync(response, Provider, new FakeTimeProvider(Now), TestContext.Current.CancellationToken);

        failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        failure.SafeMessage.ShouldNotContain("sk-live-super-secret");
        failure.SafeMessage.ShouldNotContain("alice@example.test");
        ProviderErrorMessageEvidence.TryRead(failure.Extensions).ShouldBe(hostile);
    }

    [Fact]
    public async Task CreateAsync_WhenCancelledDuringBodyRead_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await using var response = FaultingResponse(
            HttpStatusCode.InternalServerError,
            new FaultingReadStream(() =>
            {
                cancellation.Cancel();
                return new OperationCanceledException(cancellation.Token);
            }));

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => GoogleApiErrorFailureFactory.CreateAsync(response, Provider, new FakeTimeProvider(Now), cancellation.Token));
    }

    [Fact]
    public async Task CreateAsync_WhenResponseIsNull_ThrowsArgumentNullException()
    {
        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => GoogleApiErrorFailureFactory.CreateAsync(null!, Provider, new FakeTimeProvider(Now), TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("response");
    }

    [Fact]
    public async Task CreateAsync_WhenTimeProviderIsNull_ThrowsArgumentNullException()
    {
        await using var response = Response(HttpStatusCode.BadRequest);

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => GoogleApiErrorFailureFactory.CreateAsync(response, Provider, null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("timeProvider");
    }

    [Fact]
    public async Task CreateInterrupted_WhenCalled_RetainsStatusAndRetryAfterWithoutProviderCode()
    {
        await using var response = Response(HttpStatusCode.BadGateway);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(3));
        var cause = new TaskCanceledException("timed out");

        var failure = GoogleApiErrorFailureFactory.CreateInterrupted(response, Provider, ProviderFailureKind.Timeout, "Interrupted.", cause, new FakeTimeProvider(Now));

        failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        failure.ProviderId.ShouldBe(Provider);
        failure.StatusCode.ShouldBe(502);
        failure.ProviderCode.ShouldBeNull();
        failure.RetryAfter.ShouldBe(TimeSpan.FromSeconds(3));
        failure.SafeMessage.ShouldBe("Interrupted.");
        failure.DiagnosticCause.ShouldBeSameAs(cause);
        failure.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void CreateInterrupted_WhenResponseIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => GoogleApiErrorFailureFactory.CreateInterrupted(null!, Provider, ProviderFailureKind.Timeout, "Interrupted.", null, new FakeTimeProvider(Now)));

        exception.ParamName.ShouldBe("response");
    }

    [Fact]
    public async Task CreateInterrupted_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        await using var response = Response(HttpStatusCode.BadGateway);

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => GoogleApiErrorFailureFactory.CreateInterrupted(response, Provider, (ProviderFailureKind) 9999, "Interrupted.", null, new FakeTimeProvider(Now)));

        exception.ParamName.ShouldBe("kind");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateInterrupted_WhenSafeMessageIsNullOrWhiteSpace_ThrowsArgumentException(string? safeMessage)
    {
        await using var response = Response(HttpStatusCode.BadGateway);

        var exception = Should.Throw<ArgumentException>(
            () => GoogleApiErrorFailureFactory.CreateInterrupted(response, Provider, ProviderFailureKind.Timeout, safeMessage!, null, new FakeTimeProvider(Now)));

        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public async Task CreateInterrupted_WhenTimeProviderIsNull_ThrowsArgumentNullException()
    {
        await using var response = Response(HttpStatusCode.BadGateway);

        var exception = Should.Throw<ArgumentNullException>(
            () => GoogleApiErrorFailureFactory.CreateInterrupted(response, Provider, ProviderFailureKind.Timeout, "Interrupted.", null, null!));

        exception.ParamName.ShouldBe("timeProvider");
    }
}
