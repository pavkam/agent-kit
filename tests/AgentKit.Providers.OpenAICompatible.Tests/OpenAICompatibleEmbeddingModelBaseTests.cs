// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests;

using System.Net;
using System.Net.Http;

using AgentKit.Providers.Http;
using AgentKit.Providers.OpenAICompatible.Tests.Fakes;
using AgentKit.TestSupport;

/// <summary>
/// End-to-end tests for <see cref="OpenAICompatibleEmbeddingModelBase"/>,
/// exercising the full pipeline (credential resolution, translation,
/// transport, and response parsing) against a stub HTTP handler serving
/// fixture payloads. No test in this class performs a real network call.
/// </summary>
public sealed class OpenAICompatibleEmbeddingModelBaseTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly OpenAICompatibilityProfile Profile = new(
        new Uri("https://api.openai.test/"),
        "v1/chat/completions",
        sendDeveloperRoleAsSystem: false,
        preferStreaming: false,
        includeStreamUsage: true,
        useMaxCompletionTokensField: true,
        [],
        embeddingsPath: "v1/embeddings");

    private static readonly OpenAICompatibilityProfile ProfileWithoutEmbeddings = new(
        Profile.BaseAddress,
        Profile.ChatCompletionsPath,
        Profile.SendDeveloperRoleAsSystem,
        Profile.PreferStreaming,
        Profile.IncludeStreamUsage,
        Profile.UseMaxCompletionTokensField,
        Profile.DefaultRequestHeaders);

    private static EmbeddingModelRequest CreateRequest(
        EmbeddingModelDescriptor descriptor,
        DateTimeOffset deadline,
        ProviderRequestOptions? options = null) =>
        new(
            new EmbeddingRequestContext(
                new EmbeddingRequestId(Guid.NewGuid()),
                descriptor,
                new EmbeddingRequest(
                    [new TextEmbeddingInput("hello world", null)],
                    EmbeddingPurpose.Unspecified,
                    null,
                    null,
                    EmbeddingTruncation.ProviderDefault,
                    ExtensionData.Empty)),
            attempt: 1,
            deadline,
            options ?? ProviderRequestOptions.Empty);

    private static TestEmbeddingModel CreateModel(
        HttpMessageHandler handler,
        OpenAICompatibilityProfile profile,
        IProviderCredentialSource credentials,
        EmbeddingModelDescriptor? descriptor = null,
        TimeProvider? timeProvider = null) =>
        new(
            descriptor ?? TestModels.TextEmbedding3Small,
            profile,
            new OpenAIEmbeddingRequestTranslator(),
            new OpenAIEmbeddingResponseParser(),
            credentials,
            new HttpClient(handler),
            timeProvider ?? new FakeTimeProvider(Now));

    private static CustomizingEmbeddingModel CreateCustomizingModel(
        HttpMessageHandler handler,
        IProviderCredentialSource credentials,
        ProviderAuthorizationScheme scheme,
        Action<JsonObject, EmbeddingModelRequest, EmbeddingModelDescriptor> adjust,
        EmbeddingModelDescriptor? descriptor = null) =>
        new(
            descriptor ?? TestModels.TextEmbedding3Small,
            Profile,
            new OpenAIEmbeddingRequestTranslator(),
            new OpenAIEmbeddingResponseParser(),
            credentials,
            new HttpClient(handler),
            new FakeTimeProvider(Now),
            scheme,
            adjust);

    [Fact]
    public async Task GenerateAsync_WhenSuccess_ReturnsCompletedResponseWithAuthorizationHeader()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response_float.json");
        var model = CreateModel(handler, Profile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));
        var request = CreateRequest(TestModels.TextEmbedding3Small, Now.AddMinutes(1));

        var result = await model.GenerateAsync(request, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        var vector = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>().Vector.ShouldBeOfType<DenseFloatVector>();
        vector.Values.ShouldBe([0.1f, 0.2f, 0.3f]);

        _ = handler.Requests.ShouldHaveSingleItem();
        var sentRequest = handler.Requests[0];
        sentRequest.Method.ShouldBe(HttpMethod.Post);
        sentRequest.RequestUri.ShouldBe(new Uri("https://api.openai.test/v1/embeddings"));
        sentRequest.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        sentRequest.Headers.Authorization!.Parameter.ShouldBe("sk-test");
    }

    [Fact]
    public async Task GenerateAsync_WhenDeadlineIsMoreThanFortyNineDaysAway_SucceedsInsteadOfThrowing()
    {
        // CancellationTokenSource(TimeSpan, TimeProvider) rejects any delay beyond ~49.7 days
        // (uint.MaxValue - 1 ms) with an unhandled ArgumentOutOfRangeException. A caller expressing "no
        // practical deadline" via a far-future EmbeddingModelRequest deadline must not crash the attempt.
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response_float.json");
        var model = CreateModel(handler, Profile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));
        var request = CreateRequest(TestModels.TextEmbedding3Small, Now.AddDays(365));

        var result = await model.GenerateAsync(request, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        _ = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>();
    }

    [Fact]
    public async Task GenerateAsync_WhenRequestModelIdentityDiffersFromAdapter_FailsWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response_float.json");
        var model = CreateModel(handler, Profile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));
        var requestDescriptor = TestModels.TextEmbedding3Small with { ModelId = new ModelId("different-embedding-model") };
        var request = CreateRequest(requestDescriptor, Now.AddMinutes(1));

        var result = await model.GenerateAsync(request, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        failed.Failure.SafeMessage.ShouldBe("The request model descriptor does not match the configured adapter descriptor.");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_WhenProfileHasNoEmbeddingsPath_FailsWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response_float.json");
        var model = CreateModel(handler, ProfileWithoutEmbeddings, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));
        var request = CreateRequest(TestModels.TextEmbedding3Small, Now.AddMinutes(1));

        var result = await model.GenerateAsync(request, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_WhenUnauthorized_ReturnsAuthenticationFailure()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.Unauthorized, "responses/error_401.json");
        var model = CreateModel(handler, Profile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-bad")));
        var request = CreateRequest(TestModels.TextEmbedding3Small, Now.AddMinutes(1));

        var result = await model.GenerateAsync(request, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        failed.Failure.StatusCode.ShouldBe(401);
        failed.Failure.SafeMessage.ShouldBe("The provider returned HTTP status 401.");
        ProviderErrorMessageEvidence.TryRead(failed.Failure.Extensions).ShouldBe("Incorrect API key provided.");
    }

    [Fact]
    public async Task GenerateAsync_WhenErrorBodyIsNotJson_LeavesNoProviderMessageEvidence()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>Bad Gateway</html>", Encoding.UTF8, "text/html"),
        });
        var model = CreateModel(handler, Profile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));

        var result = await model.GenerateAsync(CreateRequest(TestModels.TextEmbedding3Small, Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<EmbeddingAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failure.SafeMessage.ShouldBe("The provider returned HTTP status 502.");
        _ = failure.DiagnosticCause.ShouldBeOfType<JsonException>();
        ProviderErrorMessageEvidence.TryRead(failure.Extensions).ShouldBeNull();
    }

    [Fact]
    public async Task GenerateAsync_WhenAuthorizationSchemeIsOverridden_SendsCredentialThroughOverriddenHeader()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response_float.json");
        var model = CreateCustomizingModel(
            handler,
            new StaticProviderCredentialSource(new ApiKeyProviderCredential("resource-key")),
            ProviderAuthorizationScheme.ForApiKeyHeader("api-key"),
            static (_, _, _) => { });

        var result = await model.GenerateAsync(CreateRequest(TestModels.TextEmbedding3Small, Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        var sentRequest = handler.Requests.ShouldHaveSingleItem();
        sentRequest.Headers.GetValues("api-key").ShouldBe(["resource-key"]);
        sentRequest.Headers.Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task GenerateAsync_WhenAdjustRequestPayloadIsOverridden_SendsAdjustedBodyAfterTranslation()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response_float.json");
        var descriptor = TestModels.TextEmbedding3Small with { DeploymentId = new DeploymentId("prod-embed") };
        var request = CreateRequest(descriptor, Now.AddMinutes(1));
        EmbeddingModelRequest? observedRequest = null;
        var model = CreateCustomizingModel(
            handler,
            new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")),
            ProviderAuthorizationScheme.BearerToken,
            (payload, adjustedRequest, exposedDescriptor) =>
            {
                observedRequest = adjustedRequest;
                payload["model"] = exposedDescriptor.DeploymentId!.Value.Value;
                payload["custom_marker"] = true;
            },
            descriptor);

        var result = await model.GenerateAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        observedRequest.ShouldBeSameAs(request);
        var sentBody = JsonNode.Parse(handler.RequestBodies[0]!)!;
        sentBody["model"]!.GetValue<string>().ShouldBe("prod-embed");
        sentBody["custom_marker"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public async Task GenerateAsync_WhenPreflightFails_DoesNotInvokeAdjustRequestPayload()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response_float.json");
        var invoked = false;
        var model = CreateCustomizingModel(
            handler,
            new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")),
            ProviderAuthorizationScheme.BearerToken,
            (_, _, _) => invoked = true);
        var mismatched = CreateRequest(TestModels.TextEmbedding3Small with { ModelId = new ModelId("other") }, Now.AddMinutes(1));

        var result = await model.GenerateAsync(mismatched, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<EmbeddingAttemptFailed>().Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        invoked.ShouldBeFalse();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_WhenDeadlineAlreadyElapsed_FailsWithTimeoutWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response_float.json");
        var model = CreateModel(handler, Profile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));
        var request = CreateRequest(TestModels.TextEmbedding3Small, Now.AddSeconds(-1));

        var result = await model.GenerateAsync(request, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_WhenCallerCancelsBeforeCredentialResolution_ReturnsCancelledResult()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response_float.json");
        var model = CreateModel(handler, Profile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));
        var request = CreateRequest(TestModels.TextEmbedding3Small, Now.AddMinutes(1));

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await model.GenerateAsync(request, cts.Token);

        var cancelled = result.ShouldBeOfType<EmbeddingAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_WhenProviderRequestOptionsCarryExtensionData_ForwardsThemInSentBody()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response_float.json");
        var model = CreateModel(handler, Profile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));

        var userValue = new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes("end-user-42")]);
        var options = new ProviderRequestOptions(
            new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("user", userValue)));
        var request = CreateRequest(TestModels.TextEmbedding3Small, Now.AddMinutes(1), options);

        _ = await model.GenerateAsync(request, TestContext.Current.CancellationToken);

        var sentBody = JsonNode.Parse(handler.RequestBodies[0]!);
        sentBody!["user"]!.GetValue<string>().ShouldBe("end-user-42");
    }

    [Fact]
    public async Task GenerateAsync_WhenHttpClientTimeoutFiresWithoutCallerCancellation_ReturnsTypedTimeoutFailure()
    {
        // HttpClient.Timeout surfaces as TaskCanceledException while neither the caller token nor the deadline is cancelled.
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.",
            new TimeoutException("The operation was canceled.")));
        var model = CreateModel(handler, Profile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));

        var result = await model.GenerateAsync(CreateRequest(TestModels.TextEmbedding3Small, Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<TaskCanceledException>();
    }

    [Fact]
    public async Task GenerateAsync_WhenConnectionIsRefused_ReturnsTypedUnavailableFailure()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("Connection refused", new System.Net.Sockets.SocketException(61)));
        var model = CreateModel(handler, Profile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));

        var result = await model.GenerateAsync(CreateRequest(TestModels.TextEmbedding3Small, Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task GenerateAsync_WhenCallerCancelsDuringErrorBodyRead_ReturnsCancelledResult()
    {
        var body = new GatedReadStream();
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StreamContent(body),
            };
            response.Headers.Add("x-request-id", "req_cancelled");
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(17));
            return response;
        });
        var model = CreateModel(handler, Profile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));
        using var cancellation = new CancellationTokenSource();

        var pending = model.GenerateAsync(CreateRequest(TestModels.TextEmbedding3Small, Now.AddMinutes(1)), cancellation.Token);
        (await Task.WhenAny(body.Entered, pending)).ShouldBe(body.Entered);
        await cancellation.CancelAsync();
        var result = await pending;

        var cancelled = result.ShouldBeOfType<EmbeddingAttemptCancelled>().Cancellation;
        cancelled.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        cancelled.StatusCode.ShouldBe(429);
        cancelled.RequestId.ShouldBe(new ProviderRequestId("req_cancelled"));
        cancelled.RetryAfter.ShouldBe(TimeSpan.FromSeconds(17));
    }

    [Fact]
    public async Task GenerateAsync_WhenErrorBodyReadFails_ReturnsStatusOnlyFailure()
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StreamContent(FaultingReadStream.ConnectionReset("{\"error\":"u8.ToArray())),
            };
            response.Headers.Add("x-request-id", "req_reset");
            return response;
        });
        var model = CreateModel(handler, Profile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));

        var result = await model.GenerateAsync(CreateRequest(TestModels.TextEmbedding3Small, Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<EmbeddingAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failure.StatusCode.ShouldBe(500);
        failure.RequestId.ShouldBe(new ProviderRequestId("req_reset"));
        failure.ProviderCode.ShouldBeNull();
        failure.SafeMessage.ShouldBe("The provider returned HTTP status 500.");
        _ = failure.DiagnosticCause.ShouldBeOfType<IOException>();
    }

    [Fact]
    public async Task GenerateAsync_WhenBodyStreamFailsMidRead_ReturnsTypedUnavailableFailure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(FaultingReadStream.ConnectionReset("{\"object\":\"list\",\"data\":["u8.ToArray())),
        });
        var model = CreateModel(handler, Profile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));

        var result = await model.GenerateAsync(CreateRequest(TestModels.TextEmbedding3Small, Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<IOException>();
    }

    [Fact]
    public async Task GenerateAsync_WhenOAuthCredentialHasExpired_ReturnsAuthenticationFailureWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response_float.json");
        var credential = new OAuthTokenProviderCredential("expired-token", Now.AddMinutes(-1));
        var model = CreateModel(handler, Profile, new StaticProviderCredentialSource(credential));

        var result = await model.GenerateAsync(CreateRequest(TestModels.TextEmbedding3Small, Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        failed.Failure.SafeMessage.ShouldBe("The configured OAuth access token has expired.");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_WhenTranslatorRejectsUnsupportedPurpose_ReturnsInvalidRequestFailureWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response_float.json");
        var model = CreateModel(handler, Profile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));
        var context = new EmbeddingRequestContext(
            new EmbeddingRequestId(Guid.NewGuid()),
            TestModels.TextEmbedding3Small,
            new EmbeddingRequest(
                [new TextEmbeddingInput("hello world", null)],
                EmbeddingPurpose.Query,
                null,
                null,
                EmbeddingTruncation.ProviderDefault,
                ExtensionData.Empty));
        var request = new EmbeddingModelRequest(context, attempt: 1, Now.AddMinutes(1), ProviderRequestOptions.Empty);

        var result = await model.GenerateAsync(request, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<NotSupportedException>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_WhenCallerCancelsWhileSendIsInFlight_ReturnsCancelledResult()
    {
        var handler = new GatedSendHttpMessageHandler();
        var model = CreateModel(handler, Profile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));
        using var cancellation = new CancellationTokenSource();

        var pending = model.GenerateAsync(CreateRequest(TestModels.TextEmbedding3Small, Now.AddMinutes(1)), cancellation.Token);
        (await Task.WhenAny(handler.Entered, pending)).ShouldBe(handler.Entered);
        await cancellation.CancelAsync();
        var result = await pending;

        var cancelled = result.ShouldBeOfType<EmbeddingAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
    }

    [Fact]
    public async Task GenerateAsync_WhenDeadlineExpiresWhileSendIsInFlight_ReturnsTimeoutFailure()
    {
        var clock = new FakeTimeProvider(Now);
        var handler = new GatedSendHttpMessageHandler();
        var model = CreateModel(handler, Profile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")), timeProvider: clock);

        var pending = model.GenerateAsync(CreateRequest(TestModels.TextEmbedding3Small, Now.AddSeconds(1)), TestContext.Current.CancellationToken);
        (await Task.WhenAny(handler.Entered, pending)).ShouldBe(handler.Entered);
        clock.Advance(TimeSpan.FromSeconds(2));
        var result = await pending;

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
    }

    [Fact]
    public async Task GenerateAsync_WhenDeadlineExpiresDuringErrorBodyRead_ReturnsTimeoutWithStatus()
    {
        var clock = new FakeTimeProvider(Now);
        var body = new GatedReadStream();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StreamContent(body),
        });
        var model = CreateModel(handler, Profile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")), timeProvider: clock);

        var pending = model.GenerateAsync(CreateRequest(TestModels.TextEmbedding3Small, Now.AddSeconds(1)), TestContext.Current.CancellationToken);
        (await Task.WhenAny(body.Entered, pending)).ShouldBe(body.Entered);
        clock.Advance(TimeSpan.FromSeconds(2));
        var result = await pending;

        var failure = result.ShouldBeOfType<EmbeddingAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        failure.StatusCode.ShouldBe(503);
    }

    [Fact]
    public async Task GenerateAsync_WhenTransportTimesOutDuringErrorBodyRead_ReturnsTimeoutWithStatus()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StreamContent(new FaultingReadStream(static () => new TaskCanceledException("The transport timed out.", new TimeoutException()))),
        });
        var model = CreateModel(handler, Profile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));

        var result = await model.GenerateAsync(CreateRequest(TestModels.TextEmbedding3Small, Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<EmbeddingAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        failure.StatusCode.ShouldBe(502);
        _ = failure.DiagnosticCause.ShouldBeOfType<TaskCanceledException>();
    }

    [Fact]
    public async Task GenerateAsync_WhenCallerCancelsDuringSuccessBodyRead_ReturnsCancelledResult()
    {
        var body = new GatedReadStream();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(body),
        });
        var model = CreateModel(handler, Profile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));
        using var cancellation = new CancellationTokenSource();

        var pending = model.GenerateAsync(CreateRequest(TestModels.TextEmbedding3Small, Now.AddMinutes(1)), cancellation.Token);
        (await Task.WhenAny(body.Entered, pending)).ShouldBe(body.Entered);
        await cancellation.CancelAsync();
        var result = await pending;

        var cancelled = result.ShouldBeOfType<EmbeddingAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
    }

    [Fact]
    public async Task GenerateAsync_WhenDeadlineExpiresDuringSuccessBodyRead_ReturnsTimeoutFailure()
    {
        var clock = new FakeTimeProvider(Now);
        var body = new GatedReadStream();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(body),
        });
        var model = CreateModel(handler, Profile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")), timeProvider: clock);

        var pending = model.GenerateAsync(CreateRequest(TestModels.TextEmbedding3Small, Now.AddSeconds(1)), TestContext.Current.CancellationToken);
        (await Task.WhenAny(body.Entered, pending)).ShouldBe(body.Entered);
        clock.Advance(TimeSpan.FromSeconds(2));
        var result = await pending;

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
    }

    [Fact]
    public void Alias_ReturnsDescriptorAlias()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response_float.json");
        var model = CreateModel(handler, Profile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));

        model.Alias.ShouldBe(TestModels.TextEmbedding3Small.Alias);
    }

    [Fact]
    public async Task GenerateAsync_WhenProfileHasDefaultRequestHeaders_ForwardsThemOnTheSentRequest()
    {
        var profile = new OpenAICompatibilityProfile(
            Profile.BaseAddress,
            Profile.ChatCompletionsPath,
            Profile.SendDeveloperRoleAsSystem,
            Profile.PreferStreaming,
            Profile.IncludeStreamUsage,
            Profile.UseMaxCompletionTokensField,
            ImmutableDictionary<string, string>.Empty.Add("x-provider-extra", "extra-value"),
            Profile.EmbeddingsPath);
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response_float.json");
        var model = CreateModel(handler, profile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));

        _ = await model.GenerateAsync(CreateRequest(TestModels.TextEmbedding3Small, Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var sentRequest = handler.Requests.ShouldHaveSingleItem();
        sentRequest.Headers.GetValues("x-provider-extra").ShouldBe(["extra-value"]);
    }

    [Fact]
    public async Task GenerateAsync_WhenTransportTimesOutDuringSuccessBodyRead_ReturnsTypedTimeoutFailure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new FaultingReadStream(static () => new TaskCanceledException("The transport timed out.", new TimeoutException()))),
        });
        var model = CreateModel(handler, Profile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));

        var result = await model.GenerateAsync(CreateRequest(TestModels.TextEmbedding3Small, Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<TaskCanceledException>();
    }
}
