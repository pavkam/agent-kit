// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests.Parsing;

/// <summary>
/// Verifies <see cref="OpenAIEmbeddingResponseParser.ParseAsync"/> against
/// fixture OpenAI-compatible embeddings response bodies.
/// </summary>
public sealed class OpenAIEmbeddingResponseParserTests
{
    private static EmbeddingResponseParseContext CreateContext(EmbeddingRequestId requestId, ProviderRequestId? providerRequestId = null) =>
        new(
            requestId,
            new ProviderId("openai"),
            new ApiFamilyId("openai-embeddings"),
            new ModelId("text-embedding-3-small"),
            deploymentId: null,
            providerRequestId);

    [Fact]
    public async Task ParseAsync_WhenFloatEncodedResponse_DecodesDenseFloatVector()
    {
        var requestId = new EmbeddingRequestId(Guid.NewGuid());
        var parser = new OpenAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response_float.json"));
        var result = await parser.ParseAsync(body, CreateContext(requestId), inputs, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        completed.Response.Items.Length.ShouldBe(1);

        var succeeded = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>();
        succeeded.InputIndex.ShouldBe(0);
        var vector = succeeded.Vector.ShouldBeOfType<DenseFloatVector>();
        vector.Values.ShouldBe([0.1f, 0.2f, 0.3f]);
        succeeded.Space.Dimensions.ShouldBe(3);
        succeeded.Space.ElementType.ShouldBe(EmbeddingElementType.Float32);

        completed.Response.Usage.ReportState.ShouldBe(ModelUsageReportState.Final);
        completed.Response.Usage.InputTokens.ShouldBe(5);
    }

    [Fact]
    public async Task ParseAsync_WhenBase64EncodedResponse_DecodesSameVectorAsFloatEncoding()
    {
        var requestId = new EmbeddingRequestId(Guid.NewGuid());
        var parser = new OpenAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response_base64.json"));
        var result = await parser.ParseAsync(body, CreateContext(requestId), inputs, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        var succeeded = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>();
        var vector = succeeded.Vector.ShouldBeOfType<DenseFloatVector>();

        vector.Values[0].ShouldBe(0.1f, 0.0001f);
        vector.Values[1].ShouldBe(0.2f, 0.0001f);
        vector.Values[2].ShouldBe(0.3f, 0.0001f);
    }

    [Fact]
    public async Task ParseAsync_WhenItemsArriveOutOfOrder_UsesIndexNotArrayPositionForCorrelation()
    {
        var requestId = new EmbeddingRequestId(Guid.NewGuid());
        var parser = new OpenAIEmbeddingResponseParser();
        var firstId = new EmbeddingInputId(Guid.NewGuid());
        var secondId = new EmbeddingInputId(Guid.NewGuid());
        var inputs = ImmutableArray.Create<EmbeddingInput>(
            new TextEmbeddingInput("first", firstId),
            new TextEmbeddingInput("second", secondId));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response_batch.json"));
        var result = await parser.ParseAsync(body, CreateContext(requestId), inputs, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        completed.Response.Items.Length.ShouldBe(2);

        var byIndex = completed.Response.Items.ToDictionary(item => item.InputIndex);
        byIndex[0].CorrelationId.ShouldBe(firstId);
        byIndex[1].CorrelationId.ShouldBe(secondId);

        var firstVector = byIndex[0].ShouldBeOfType<EmbeddingItemSucceeded>().Vector.ShouldBeOfType<DenseFloatVector>();
        firstVector.Values.ShouldBe([0.1f, 0.2f]);

        var secondVector = byIndex[1].ShouldBeOfType<EmbeddingItemSucceeded>().Vector.ShouldBeOfType<DenseFloatVector>();
        secondVector.Values.ShouldBe([0.4f, 0.5f]);
    }

    [Fact]
    public async Task ParseAsync_WhenBodyIsNotJson_FailsWithProtocolViolation()
    {
        var requestId = new EmbeddingRequestId(Guid.NewGuid());
        var parser = new OpenAIEmbeddingResponseParser();

        await using var body = new MemoryStream("not json"u8.ToArray());
        var result = await parser.ParseAsync(body, CreateContext(requestId), [], TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<JsonException>();
    }

    [Fact]
    public async Task ParseAsync_WhenNoDataItems_FailsWithProtocolViolation()
    {
        var requestId = new EmbeddingRequestId(Guid.NewGuid());
        var parser = new OpenAIEmbeddingResponseParser();

        await using var body = new MemoryStream(
            /*lang=json,strict*/ """{"object":"list","data":[],"model":"text-embedding-3-small"}"""u8.ToArray());
        var result = await parser.ParseAsync(body, CreateContext(requestId), [], TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task ParseAsync_WhenItemCountDoesNotMatchInputCount_ReturnsProtocolFailure(int itemCount)
    {
        var requestId = new EmbeddingRequestId(Guid.NewGuid());
        var parser = new OpenAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(
            new TextEmbeddingInput("first", null),
            new TextEmbeddingInput("second", null));

        var items = string.Join(
            ",",
            Enumerable.Range(0, itemCount).Select(index => $$"""{"object":"embedding","index":{{index}},"embedding":[0.1,0.2]}"""));
        await using var body = new MemoryStream(
            Encoding.UTF8.GetBytes($$"""{"object":"list","data":[{{items}}],"model":"text-embedding-3-small"}"""));

        var result = await parser.ParseAsync(body, CreateContext(requestId), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Fact]
    public async Task ParseAsync_WhenAVectorElementIsNotANumber_ReturnsProtocolFailureInsteadOfThrowing()
    {
        // JsonElement.GetSingle() throws InvalidOperationException (not JsonException) for a non-Number
        // element; that must not escape the parser uncaught for a misbehaving compatible server that
        // sends null, a string, or a nested array for a vector element.
        var requestId = new EmbeddingRequestId(Guid.NewGuid());
        var parser = new OpenAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("only", null));

        await using var body = new MemoryStream(
            /*lang=json,strict*/ """{"object":"list","data":[{"object":"embedding","index":0,"embedding":[0.1,"not-a-number",0.3]}],"model":"text-embedding-3-small"}"""u8.ToArray());

        var result = await parser.ParseAsync(body, CreateContext(requestId), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Fact]
    public async Task ParseAsync_WhenAnItemIndexIsOutOfRange_ReturnsProtocolFailure()
    {
        var requestId = new EmbeddingRequestId(Guid.NewGuid());
        var parser = new OpenAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("only", null));

        await using var body = new MemoryStream(
            /*lang=json,strict*/ """{"object":"list","data":[{"object":"embedding","index":5,"embedding":[0.1,0.2]}],"model":"text-embedding-3-small"}"""u8.ToArray());

        var result = await parser.ParseAsync(body, CreateContext(requestId), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Fact]
    public async Task ParseAsync_WhenTwoItemsShareTheSameIndex_ReturnsProtocolFailure()
    {
        var requestId = new EmbeddingRequestId(Guid.NewGuid());
        var parser = new OpenAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(
            new TextEmbeddingInput("first", null),
            new TextEmbeddingInput("second", null));

        await using var body = new MemoryStream(
            /*lang=json,strict*/ """
            {
              "object": "list",
              "data": [
                { "object": "embedding", "index": 0, "embedding": [0.1, 0.2] },
                { "object": "embedding", "index": 0, "embedding": [0.3, 0.4] }
              ],
              "model": "text-embedding-3-small"
            }
            """u8.ToArray());

        var result = await parser.ParseAsync(body, CreateContext(requestId), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Fact]
    public async Task ParseAsync_WhenBase64EmbeddingLengthIsNotAMultipleOfFour_ReturnsProtocolFailure()
    {
        var requestId = new EmbeddingRequestId(Guid.NewGuid());
        var parser = new OpenAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        // "AQID" base64-decodes to exactly 3 raw bytes: not a whole number of 4-byte singles.
        await using var body = new MemoryStream(
            /*lang=json,strict*/ """{"object":"list","data":[{"object":"embedding","index":0,"embedding":"AQID"}],"model":"text-embedding-3-small"}"""u8.ToArray());

        var result = await parser.ParseAsync(body, CreateContext(requestId), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<FormatException>();
    }

    [Fact]
    public async Task ParseAsync_WhenSucceeding_ReportsProviderRequestIdOnResponseAndIdentity()
    {
        var requestId = new EmbeddingRequestId(Guid.NewGuid());
        var providerRequestId = new ProviderRequestId("req_openai_123");
        var parser = new OpenAIEmbeddingResponseParser();
        var firstId = new EmbeddingInputId(Guid.NewGuid());
        var secondId = new EmbeddingInputId(Guid.NewGuid());
        var inputs = ImmutableArray.Create<EmbeddingInput>(
            new TextEmbeddingInput("first", firstId),
            new TextEmbeddingInput("second", secondId));

        // The wire returns index 1 before index 0; items must still be reported in input order.
        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response_batch.json"));

        var result = await parser.ParseAsync(
            body, CreateContext(requestId, providerRequestId), inputs, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        completed.Response.ProviderRequestId.ShouldBe(providerRequestId);

        completed.Response.Items.Length.ShouldBe(2);
        completed.Response.Items[0].InputIndex.ShouldBe(0);
        completed.Response.Items[0].CorrelationId.ShouldBe(firstId);
        completed.Response.Items[1].InputIndex.ShouldBe(1);
        completed.Response.Items[1].CorrelationId.ShouldBe(secondId);

        var succeeded = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>();
        succeeded.Space.Provider.RequestId.ShouldBe(providerRequestId);
    }

    [Fact]
    public async Task ParseAsync_WhenEmbeddingFieldIsNeitherArrayNorString_ReturnsProtocolFailure()
    {
        var requestId = new EmbeddingRequestId(Guid.NewGuid());
        var parser = new OpenAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = new MemoryStream(
            /*lang=json,strict*/ """{"object":"list","data":[{"object":"embedding","index":0,"embedding":42}],"model":"text-embedding-3-small"}"""u8.ToArray());

        var result = await parser.ParseAsync(body, CreateContext(requestId), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<JsonException>();
    }

    [Fact]
    public async Task ParseAsync_WhenUsageHasNegativePromptTokens_ReturnsProtocolFailure()
    {
        var requestId = new EmbeddingRequestId(Guid.NewGuid());
        var parser = new OpenAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = new MemoryStream(
            /*lang=json,strict*/ """{"object":"list","data":[{"object":"embedding","index":0,"embedding":[0.1,0.2]}],"model":"text-embedding-3-small","usage":{"prompt_tokens":-1,"total_tokens":0}}"""u8.ToArray());

        var result = await parser.ParseAsync(body, CreateContext(requestId), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider returned invalid usage evidence.");
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ParseAsync_WhenFailing_ReportsProviderRequestIdOnFailure()
    {
        var requestId = new EmbeddingRequestId(Guid.NewGuid());
        var providerRequestId = new ProviderRequestId("req_openai_failure");
        var parser = new OpenAIEmbeddingResponseParser();

        await using var body = new MemoryStream("not json"u8.ToArray());
        var result = await parser.ParseAsync(
            body, CreateContext(requestId, providerRequestId), [], TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.RequestId.ShouldBe(providerRequestId);
    }
}
