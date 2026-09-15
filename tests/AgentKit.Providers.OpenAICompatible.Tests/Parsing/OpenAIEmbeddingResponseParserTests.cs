// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests.Parsing;

/// <summary>
/// Verifies <see cref="OpenAIEmbeddingResponseParser.ParseAsync"/> against
/// fixture OpenAI-compatible embeddings response bodies.
/// </summary>
public sealed class OpenAIEmbeddingResponseParserTests
{
    private static EmbeddingResponseParseContext CreateContext(EmbeddingRequestId requestId) =>
        new(
            requestId,
            new ProviderId("openai"),
            new ApiFamilyId("openai-embeddings"),
            new ModelId("text-embedding-3-small"),
            deploymentId: null,
            providerRequestId: null);

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
}
