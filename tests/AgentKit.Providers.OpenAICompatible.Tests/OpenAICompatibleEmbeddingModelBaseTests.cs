// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests;

using System.Net;
using System.Net.Http;

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
}
