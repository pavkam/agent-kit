// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Tests;

using System.Net;
using System.Net.Http;

using AgentKit.Providers.AwsBedrock.Tests.Fakes;

/// <summary>Verifies AwsBedrockLlmModel behavior and contracts.</summary>
public sealed class AwsBedrockLlmModelTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static LlmModelRequest CreateRequest(ModelDescriptor descriptor, DateTimeOffset deadline, ImmutableArray<LlmToolDefinition> tools = default, ProviderRequestOptions? options = null)
    {
        var context = new LlmRequestContext(new ModelRequestId(Guid.NewGuid()), descriptor, [TestMessages.User("Hello!")], tools.IsDefault ? [] : tools, LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        return new LlmModelRequest(context, attempt: 1, deadline, options ?? ProviderRequestOptions.Empty);
    }

    private static AwsBedrockLlmModel CreateModel(HttpMessageHandler handler, IAwsCredentialSource credentials, ModelDescriptor? descriptor = null, TimeProvider? timeProvider = null, AwsBedrockProviderOptions? options = null) => new(descriptor ?? TestModels.ClaudeSonnet, options ?? new AwsBedrockProviderOptions { Region = "us-east-1" }, new AwsBedrockRequestTranslator(), new AwsBedrockResponseParser(new SequentialToolCallIdGenerator()), credentials, new HttpClient(handler), timeProvider ?? new FakeTimeProvider(Now));
    private static StaticAwsCredentialSource CreateCredentials() => new(new AwsSigV4Credential("AKIAIOSFODNN7EXAMPLE", "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY", null));
    [Fact]
    public async Task ExecuteAsync_WhenNonStreamingSuccess_SendsSignedConverseRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = false
        };
        var model = CreateModel(handler, CreateCredentials(), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello! How can I help you today?");
        _ = handler.Requests.ShouldHaveSingleItem();
        var sentRequest = handler.Requests[0];
        sentRequest.Method.ShouldBe(HttpMethod.Post);
        sentRequest.RequestUri.ShouldBe(new Uri("https://bedrock-runtime.us-east-1.amazonaws.com/model/anthropic.claude-3-sonnet-20240229-v1%3A0/converse"));
        _ = sentRequest.Headers.GetValues("x-amz-date").ShouldHaveSingleItem();
        _ = sentRequest.Headers.GetValues("x-amz-content-sha256").ShouldHaveSingleItem();
        var authorization = sentRequest.Headers.GetValues("Authorization").ShouldHaveSingleItem();
        authorization.ShouldStartWith("AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/");
        authorization.ShouldContain("bedrock/aws4_request");
        sentRequest.Headers.Contains("x-amz-security-token").ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenSessionTokenCredential_IncludesSecurityTokenHeader()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = false
        };
        var credentials = new StaticAwsCredentialSource(new AwsSigV4Credential("AKIAIOSFODNN7EXAMPLE", "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY", "session-token-value"));
        var model = CreateModel(handler, credentials, options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        _ = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        handler.Requests[0].Headers.GetValues("x-amz-security-token").ShouldContain("session-token-value");
    }

    [Fact]
    public async Task ExecuteAsync_WhenStreamingSuccess_ReturnsCompletedResponseFromConverseStreamPath()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/streaming_text.bin", "application/vnd.amazon.eventstream");
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = true
        };
        var model = CreateModel(handler, CreateCredentials(), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello world");
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldEndWith("/converse-stream");
    }

    [Fact]
    public async Task ExecuteAsync_WhenAccessDenied_ReturnsAuthorizationFailureUsingErrorTypeHeader()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.Forbidden, "responses/error_access_denied.json", configureHeaders: response => response.Headers.Add("x-amzn-errortype", "AccessDeniedException"));
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = false
        };
        var model = CreateModel(handler, CreateCredentials(), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Authorization);
        failed.Failure.StatusCode.ShouldBe(403);
        failed.Failure.SafeMessage.ShouldBe("User is not authorized to perform: bedrock:InvokeModel");
        failed.Failure.ProviderCode.ShouldBe("AccessDeniedException");
    }

    [Fact]
    public async Task ExecuteAsync_WhenThrottled_ReturnsThrottlingFailure()
    {
        var handler = StubHttpMessageHandler.FromFixture((HttpStatusCode) 429, "responses/error_throttling.json", configureHeaders: response => response.Headers.Add("x-amzn-errortype", "ThrottlingException"));
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = false
        };
        var model = CreateModel(handler, CreateCredentials(), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Throttling);
    }

    [Fact]
    public async Task ExecuteAsync_WhenErrorHasNoErrorTypeHeader_FallsBackToStatusCodeMapping()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.InternalServerError, "responses/error_throttling.json");
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = false
        };
        var model = CreateModel(handler, CreateCredentials(), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failed.Failure.ProviderCode.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenModelDoesNotSupportRequestedTools_FailsWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = false
        };
        var model = CreateModel(handler, CreateCredentials(), descriptor: TestModels.NoToolSupport, options: options);
        var tools = ImmutableArray.Create(new LlmToolDefinition(new ToolId("get_weather"), "get_weather", null, JsonDocument.Parse("{}").RootElement));
        var request = CreateRequest(TestModels.NoToolSupport, Now.AddMinutes(1), tools);
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeadlineAlreadyElapsed_FailsWithTimeoutWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = false
        };
        var model = CreateModel(handler, CreateCredentials(), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddSeconds(-1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallerCancelsBeforeCredentialResolution_ReturnsCancelledResult()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = false
        };
        var model = CreateModel(handler, CreateCredentials(), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var result = await model.ExecuteAsync(request, observer, cts.Token);
        var cancelled = result.ShouldBeOfType<ModelAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        handler.Requests.ShouldBeEmpty();
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseCancelled>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledMidStream_ReturnsCancelledWithoutEverCompletingTheResponse()
    {
        var payload = TestResources.ReadAllBytes("responses/streaming_text.bin");
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new DelayedStream(payload)), });
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = true
        };
        var model = CreateModel(handler, CreateCredentials(), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
        using var cts = new CancellationTokenSource();
        var innerObserver = new RecordingModelResponseObserver();
        var cancelingObserver = new CancelingModelResponseObserver(innerObserver, cts, cancelAfterEventCount: 1);
        var result = await model.ExecuteAsync(request, cancelingObserver, cts.Token);
        var cancelled = result.ShouldBeOfType<ModelAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        innerObserver.Events.ShouldNotContain(e => e is ModelResponseCompleted);
        _ = innerObserver.Events[^1].ShouldBeOfType<ModelResponseCancelled>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderRequestOptionsCarryExtensionData_ForwardsThemInSentBody()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = false
        };
        var model = CreateModel(handler, CreateCredentials(), options: options);
        var extensionValue = new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(new { latency = "optimized" })]);
        var providerOptions = new ProviderRequestOptions(new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("performanceConfig", extensionValue)));
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1), options: providerOptions);
        var observer = new RecordingModelResponseObserver();
        _ = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var sentBody = JsonNode.Parse(handler.RequestBodies[0].AsSpan());
        sentBody!["performanceConfig"]!["latency"]!.GetValue<string>().ShouldBe("optimized");
    }
}
