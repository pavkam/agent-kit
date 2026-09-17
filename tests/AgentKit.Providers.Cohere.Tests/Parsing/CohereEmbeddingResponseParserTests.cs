// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Tests.Parsing;

using System.Diagnostics;
using System.Text;

/// <summary>
/// Verifies <see cref="CohereEmbeddingResponseParser.ParseAsync"/> against
/// fixture Cohere v2 embed response bodies, across every supported
/// <c>embedding_types</c> encoding, and its positional (index-free)
/// input-to-vector correlation.
/// </summary>
public sealed class CohereEmbeddingResponseParserTests
{
    private static CohereEmbeddingResponseParseContext CreateContext(
        EmbeddingEncoding requestedEncoding, EmbeddingPurpose requestedPurpose = EmbeddingPurpose.Document) =>
        new(
            new EmbeddingRequestId(Guid.NewGuid()),
            CohereProviderDefaults.ProviderId,
            CohereProviderDefaults.EmbeddingApiFamily,
            new ModelId("embed-v4.0"),
            requestedEncoding,
            requestedPurpose);

    private static CohereEmbeddingResponseParseContext CreateContext(EmbeddingEncoding requestedEncoding, ProviderRequestId providerRequestId) =>
        CreateContext(requestedEncoding) with { ProviderRequestId = providerRequestId };

    [Fact]
    public async Task ParseAsync_WhenFloatEncoding_DecodesDenseFloatVectorsPositionally()
    {
        var parser = new CohereEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(
            new TextEmbeddingInput("hello", new EmbeddingInputId(Guid.Parse("00000000-0000-0000-0000-00000000000a"))),
            new TextEmbeddingInput("world", new EmbeddingInputId(Guid.Parse("00000000-0000-0000-0000-00000000000b"))));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response_float.json"));
        var result = await parser.ParseAsync(
            body, CreateContext(EmbeddingEncoding.Float), inputs, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        completed.Response.Items.Length.ShouldBe(2);

        var first = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>();
        first.InputIndex.ShouldBe(0);
        first.CorrelationId.ShouldBe(new EmbeddingInputId(Guid.Parse("00000000-0000-0000-0000-00000000000a")));
        first.Vector.ShouldBeOfType<DenseFloatVector>().Values.ShouldBe([0.1f, 0.2f, 0.3f]);

        var second = completed.Response.Items[1].ShouldBeOfType<EmbeddingItemSucceeded>();
        second.InputIndex.ShouldBe(1);
        second.CorrelationId.ShouldBe(new EmbeddingInputId(Guid.Parse("00000000-0000-0000-0000-00000000000b")));
        second.Vector.ShouldBeOfType<DenseFloatVector>().Values.ShouldBe([0.4f, 0.5f, 0.6f]);

        completed.Response.Usage.ReportState.ShouldBe(ModelUsageReportState.Final);
        completed.Response.Usage.InputTokens.ShouldBe(5);
    }

    [Fact]
    public async Task ParseAsync_WhenFloatEncoding_SetsSpaceIdentityPurposeFromRequest()
    {
        var parser = new CohereEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null), new TextEmbeddingInput("world", null));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response_float.json"));
        var result = await parser.ParseAsync(
            body, CreateContext(EmbeddingEncoding.Float, EmbeddingPurpose.Query), inputs, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        var space = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>().Space;
        space.Purpose.ShouldBe(EmbeddingPurpose.Query);
        space.ElementType.ShouldBe(EmbeddingElementType.Float32);
        space.Dimensions.ShouldBe(3);
    }

    [Fact]
    public async Task ParseAsync_WhenInt8Encoding_DecodesSignedQuantizedByteVector()
    {
        var parser = new CohereEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response_int8.json"));
        var result = await parser.ParseAsync(
            body, CreateContext(EmbeddingEncoding.Int8), inputs, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        var vector = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>().Vector.ShouldBeOfType<QuantizedByteVector>();
        vector.Signed.ShouldBeTrue();
        vector.Values.ShouldBe([unchecked((byte) -5), 0, 120, unchecked((byte) -128)]);
    }

    [Fact]
    public async Task ParseAsync_WhenUInt8Encoding_DecodesUnsignedQuantizedByteVector()
    {
        var parser = new CohereEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response_uint8.json"));
        var result = await parser.ParseAsync(
            body, CreateContext(EmbeddingEncoding.UInt8), inputs, TestContext.Current.CancellationToken);

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
        var parser = new CohereEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response_binary.json"));
        var result = await parser.ParseAsync(
            body, CreateContext(EmbeddingEncoding.Binary), inputs, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        var succeeded = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>();
        var vector = succeeded.Vector.ShouldBeOfType<PackedBinaryVector>();
        vector.Signed.ShouldBeTrue();
        vector.Values.ShouldBe([170, 5]);
        succeeded.Space.Dimensions.ShouldBe(16);
        succeeded.Space.ElementType.ShouldBe(EmbeddingElementType.Binary);
    }

    [Fact]
    public async Task ParseAsync_WhenRequestedEncodingFieldIsNotAJsonArray_FailsWithProtocolViolation()
    {
        var parser = new CohereEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));
        const string payload = /*lang=json,strict*/ """{"id":"emb-bad-shape","texts":["hello"],"embeddings":{"float":"not-an-array"}}""";

        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        var result = await parser.ParseAsync(
            body, CreateContext(EmbeddingEncoding.Float), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider response's 'embeddings.float' field was not a JSON array.");
    }

    [Fact]
    public async Task ParseAsync_WhenVectorEntryIsNotAJsonArray_FailsWithProtocolViolation()
    {
        var parser = new CohereEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));
        const string payload = /*lang=json,strict*/ """{"id":"emb-bad","texts":["hello"],"embeddings":{"float":["not-an-array"]}}""";

        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        var result = await parser.ParseAsync(
            body, CreateContext(EmbeddingEncoding.Float), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<JsonException>();
    }

    [Fact]
    public async Task ParseAsync_WhenUBinaryEncoding_DecodesUnsignedPackedBinaryVectorWithBitDimensions()
    {
        var parser = new CohereEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response_ubinary.json"));
        var result = await parser.ParseAsync(
            body, CreateContext(EmbeddingEncoding.UBinary), inputs, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        var succeeded = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>();
        var vector = succeeded.Vector.ShouldBeOfType<PackedBinaryVector>();
        vector.Signed.ShouldBeFalse();
        vector.Values.ShouldBe([170, 5]);

        succeeded.Space.Dimensions.ShouldBe(16);
        succeeded.Space.ElementType.ShouldBe(EmbeddingElementType.UBinary);
    }

    [Fact]
    public async Task ParseAsync_WhenUsageTokenCountIsNegativeFraction_FailsWithProtocolViolation()
    {
        var parser = new CohereEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(
            new TextEmbeddingInput("hello", null),
            new TextEmbeddingInput("world", null));
        var payload = TestResources.ReadAllText("responses/embedding_response_float.json")
            .Replace("\"input_tokens\": 5", "\"input_tokens\": -0.5", StringComparison.Ordinal);

        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        var result = await parser.ParseAsync(
            body, CreateContext(EmbeddingEncoding.Float), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Fact]
    public async Task ParseAsync_WhenRequestedEncodingIsUndefined_ThrowsUnreachableException()
    {
        // EmbeddingEncoding's members are all enumerated by EncodingKeyOf's switch; an undefined value can
        // only be produced by an unsafe cast, and the parser fails closed rather than guessing a wire key.
        var parser = new CohereEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));
        const string payload = /*lang=json,strict*/ """{"id":"emb-undefined","texts":["hello"],"embeddings":{"float":[[0.1]]}}""";

        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        _ = await Should.ThrowAsync<UnreachableException>(
            () => parser.ParseAsync(body, CreateContext((EmbeddingEncoding) 999), inputs, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ParseAsync_WhenBodyIsNotJson_FailsWithProtocolViolation()
    {
        var parser = new CohereEmbeddingResponseParser();

        await using var body = new MemoryStream("not json"u8.ToArray());
        var result = await parser.ParseAsync(body, CreateContext(EmbeddingEncoding.Float), [], TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<JsonException>();
    }

    [Fact]
    public async Task ParseAsync_WhenRequestedEncodingMissingFromResponse_FailsWithProtocolViolation()
    {
        var parser = new CohereEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null), new TextEmbeddingInput("world", null));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response_float.json"));
        var result = await parser.ParseAsync(
            body, CreateContext(EmbeddingEncoding.Int8), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Fact]
    public async Task ParseAsync_WhenVectorCountDoesNotMatchInputCount_FailsWithProtocolViolation()
    {
        var parser = new CohereEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(
            new TextEmbeddingInput("hello", null), new TextEmbeddingInput("world", null), new TextEmbeddingInput("extra", null));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response_float.json"));
        var result = await parser.ParseAsync(
            body, CreateContext(EmbeddingEncoding.Float), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Fact]
    public async Task ParseAsync_WhenSucceeding_ReportsProviderRequestIdOnResponseAndIdentity()
    {
        var providerRequestId = new ProviderRequestId("req_cohere_123");
        var parser = new CohereEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(
            new TextEmbeddingInput("hello", null), new TextEmbeddingInput("world", null));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response_float.json"));
        var result = await parser.ParseAsync(
            body, CreateContext(EmbeddingEncoding.Float, providerRequestId), inputs, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        completed.Response.ProviderRequestId.ShouldBe(providerRequestId);

        var succeeded = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>();
        succeeded.Space.Provider.RequestId.ShouldBe(providerRequestId);
    }

    [Fact]
    public async Task ParseAsync_WhenFailing_ReportsProviderRequestIdOnFailure()
    {
        var providerRequestId = new ProviderRequestId("req_cohere_failure");
        var parser = new CohereEmbeddingResponseParser();

        await using var body = new MemoryStream("not json"u8.ToArray());
        var result = await parser.ParseAsync(
            body, CreateContext(EmbeddingEncoding.Float, providerRequestId), [], TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.RequestId.ShouldBe(providerRequestId);
    }
}
