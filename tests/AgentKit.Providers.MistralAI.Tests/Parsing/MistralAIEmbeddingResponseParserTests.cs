// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Tests.Parsing;

/// <summary>
/// Verifies <see cref="MistralAIEmbeddingResponseParser.ParseAsync"/>
/// against fixture Mistral AI embeddings response bodies, across every
/// supported <c>output_dtype</c>.
/// </summary>
public sealed class MistralAIEmbeddingResponseParserTests
{
    private static MistralAIEmbeddingResponseParseContext CreateContext(EmbeddingEncoding? requestedEncoding) =>
        new(
            new EmbeddingRequestId(Guid.NewGuid()),
            MistralAIProviderDefaults.ProviderId,
            MistralAIProviderDefaults.EmbeddingApiFamily,
            new ModelId("mistral-embed"),
            requestedEncoding);

    private static MistralAIEmbeddingResponseParseContext CreateContext(EmbeddingEncoding? requestedEncoding, ProviderRequestId providerRequestId) =>
        CreateContext(requestedEncoding) with { ProviderRequestId = providerRequestId };

    [Fact]
    public async Task ParseAsync_WhenFloatEncoding_DecodesDenseFloatVector()
    {
        var parser = new MistralAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response_float.json"));
        var result = await parser.ParseAsync(body, CreateContext(EmbeddingEncoding.Float), inputs, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        var vector = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>().Vector.ShouldBeOfType<DenseFloatVector>();
        vector.Values.ShouldBe([0.1f, 0.2f, 0.3f]);
        completed.Response.Usage.ReportState.ShouldBe(ModelUsageReportState.Final);
        completed.Response.Usage.InputTokens.ShouldBe(5);
    }

    [Fact]
    public async Task ParseAsync_WhenNoEncodingRequested_DefaultsToDenseFloatVector()
    {
        var parser = new MistralAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response_float.json"));
        var result = await parser.ParseAsync(body, CreateContext(null), inputs, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        _ = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>().Vector.ShouldBeOfType<DenseFloatVector>();
    }

    [Fact]
    public async Task ParseAsync_WhenInt8Encoding_DecodesSignedQuantizedByteVector()
    {
        var parser = new MistralAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response_int8.json"));
        var result = await parser.ParseAsync(body, CreateContext(EmbeddingEncoding.Int8), inputs, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        var vector = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>().Vector.ShouldBeOfType<QuantizedByteVector>();
        vector.Signed.ShouldBeTrue();
        vector.Values.ShouldBe([unchecked((byte) -5), 0, 120, unchecked((byte) -128)]);

        var space = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>().Space;
        space.ElementType.ShouldBe(EmbeddingElementType.Int8);
    }

    [Fact]
    public async Task ParseAsync_WhenUBinaryEncoding_DecodesUnsignedPackedBinaryVectorWithBitDimensions()
    {
        var parser = new MistralAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response_ubinary.json"));
        var result = await parser.ParseAsync(body, CreateContext(EmbeddingEncoding.UBinary), inputs, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        var succeeded = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>();
        var vector = succeeded.Vector.ShouldBeOfType<PackedBinaryVector>();
        vector.Signed.ShouldBeFalse();
        vector.Values.ShouldBe([170, 5]);

        succeeded.Space.Dimensions.ShouldBe(16);
        succeeded.Space.ElementType.ShouldBe(EmbeddingElementType.UBinary);
    }

    [Fact]
    public async Task ParseAsync_WhenUInt8Encoding_DecodesUnsignedQuantizedByteVector()
    {
        var parser = new MistralAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = new MemoryStream(
            /*lang=json,strict*/ """{"object":"list","data":[{"object":"embedding","index":0,"embedding":[5,0,120,250]}],"model":"mistral-embed"}"""u8.ToArray());
        var result = await parser.ParseAsync(body, CreateContext(EmbeddingEncoding.UInt8), inputs, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        var succeeded = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>();
        var vector = succeeded.Vector.ShouldBeOfType<QuantizedByteVector>();
        vector.Signed.ShouldBeFalse();
        vector.Values.ShouldBe([5, 0, 120, 250]);
        succeeded.Space.ElementType.ShouldBe(EmbeddingElementType.UInt8);
    }

    [Fact]
    public async Task ParseAsync_WhenBinaryEncoding_DecodesSignedPackedBinaryVectorWithBitDimensions()
    {
        var parser = new MistralAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = new MemoryStream(
            /*lang=json,strict*/ """{"object":"list","data":[{"object":"embedding","index":0,"embedding":[-86,5]}],"model":"mistral-embed"}"""u8.ToArray());
        var result = await parser.ParseAsync(body, CreateContext(EmbeddingEncoding.Binary), inputs, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        var succeeded = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>();
        var vector = succeeded.Vector.ShouldBeOfType<PackedBinaryVector>();
        vector.Signed.ShouldBeTrue();
        vector.Values.ShouldBe([170, 5]);
        succeeded.Space.Dimensions.ShouldBe(16);
        succeeded.Space.ElementType.ShouldBe(EmbeddingElementType.Binary);
    }

    [Fact]
    public async Task ParseAsync_WhenEmbeddingFieldIsNotAJsonArray_FailsWithProtocolViolation()
    {
        var parser = new MistralAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = new MemoryStream(
            /*lang=json,strict*/ """{"object":"list","data":[{"object":"embedding","index":0,"embedding":"not-an-array"}],"model":"mistral-embed"}"""u8.ToArray());
        var result = await parser.ParseAsync(body, CreateContext(EmbeddingEncoding.Float), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        _ = failed.Failure.DiagnosticCause.ShouldBeAssignableTo<JsonException>();
    }

    [Fact]
    public async Task ParseAsync_WhenPromptTokensIsNegative_FailsWithProtocolViolation()
    {
        var parser = new MistralAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = new MemoryStream(
            /*lang=json,strict*/ """{"object":"list","data":[{"object":"embedding","index":0,"embedding":[0.1,0.2]}],"model":"mistral-embed","usage":{"prompt_tokens":-5,"total_tokens":-5}}"""u8.ToArray());
        var result = await parser.ParseAsync(body, CreateContext(EmbeddingEncoding.Float), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider returned invalid usage evidence.");
        _ = failed.Failure.DiagnosticCause.ShouldBeAssignableTo<ArgumentException>();
    }

    [Fact]
    public async Task ParseAsync_WhenBodyIsNotJson_FailsWithProtocolViolation()
    {
        var parser = new MistralAIEmbeddingResponseParser();

        await using var body = new MemoryStream("not json"u8.ToArray());
        var result = await parser.ParseAsync(body, CreateContext(null), [], TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<JsonException>();
    }

    [Fact]
    public async Task ParseAsync_WhenNoDataItems_FailsWithProtocolViolation()
    {
        var parser = new MistralAIEmbeddingResponseParser();

        await using var body = new MemoryStream(/*lang=json,strict*/ """{"object":"list","data":[],"model":"mistral-embed"}"""u8.ToArray());
        var result = await parser.ParseAsync(body, CreateContext(null), [], TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task ParseAsync_WhenItemCountDoesNotMatchInputCount_ReturnsProtocolFailure(int itemCount)
    {
        var parser = new MistralAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(
            new TextEmbeddingInput("first", null),
            new TextEmbeddingInput("second", null));

        var items = string.Join(
            ",",
            Enumerable.Range(0, itemCount).Select(index => $$"""{"object":"embedding","index":{{index}},"embedding":[0.1,0.2]}"""));
        await using var body = new MemoryStream(
            Encoding.UTF8.GetBytes($$"""{"object":"list","data":[{{items}}],"model":"mistral-embed"}"""));

        var result = await parser.ParseAsync(body, CreateContext(null), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Fact]
    public async Task ParseAsync_WhenAnItemIndexIsOutOfRange_ReturnsProtocolFailure()
    {
        var parser = new MistralAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("only", null));

        await using var body = new MemoryStream(
            /*lang=json,strict*/ """{"object":"list","data":[{"object":"embedding","index":5,"embedding":[0.1,0.2]}],"model":"mistral-embed"}"""u8.ToArray());

        var result = await parser.ParseAsync(body, CreateContext(null), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Fact]
    public async Task ParseAsync_WhenTwoItemsShareTheSameIndex_ReturnsProtocolFailure()
    {
        var parser = new MistralAIEmbeddingResponseParser();
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
              "model": "mistral-embed"
            }
            """u8.ToArray());

        var result = await parser.ParseAsync(body, CreateContext(null), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Fact]
    public async Task ParseAsync_WhenSucceeding_ReportsProviderRequestIdOnResponseAndIdentity()
    {
        var providerRequestId = new ProviderRequestId("req_mistral_123");
        var parser = new MistralAIEmbeddingResponseParser();
        var firstId = new EmbeddingInputId(Guid.NewGuid());
        var secondId = new EmbeddingInputId(Guid.NewGuid());
        var inputs = ImmutableArray.Create<EmbeddingInput>(
            new TextEmbeddingInput("first", firstId),
            new TextEmbeddingInput("second", secondId));

        // The wire returns index 1 before index 0; items must still be reported in input order.
        await using var body = new MemoryStream(
            /*lang=json,strict*/ """
            {
              "object": "list",
              "data": [
                { "object": "embedding", "index": 1, "embedding": [0.4, 0.5] },
                { "object": "embedding", "index": 0, "embedding": [0.1, 0.2] }
              ],
              "model": "mistral-embed",
              "usage": { "prompt_tokens": 8, "total_tokens": 8 }
            }
            """u8.ToArray());

        var result = await parser.ParseAsync(
            body, CreateContext(EmbeddingEncoding.Float, providerRequestId), inputs, TestContext.Current.CancellationToken);

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
    public async Task ParseAsync_WhenFailing_ReportsProviderRequestIdOnFailure()
    {
        var providerRequestId = new ProviderRequestId("req_mistral_failure");
        var parser = new MistralAIEmbeddingResponseParser();

        await using var body = new MemoryStream("not json"u8.ToArray());
        var result = await parser.ParseAsync(
            body, CreateContext(null, providerRequestId), [], TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.RequestId.ShouldBe(providerRequestId);
    }
}
