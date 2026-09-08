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
}
