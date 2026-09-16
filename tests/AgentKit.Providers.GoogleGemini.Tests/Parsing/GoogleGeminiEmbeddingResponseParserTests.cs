// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Tests.Parsing;

/// <summary>
/// Verifies <see cref="GoogleGeminiEmbeddingResponseParser.ParseAsync"/>
/// against fixture Gemini <c>batchEmbedContents</c> response bodies.
/// </summary>
public sealed class GoogleGeminiEmbeddingResponseParserTests
{
    private static EmbeddingResponseParseContext CreateContext(EmbeddingRequestId requestId, ProviderRequestId? providerRequestId = null) =>
        new(
            requestId,
            GoogleGeminiProviderDefaults.ProviderId,
            GoogleGeminiProviderDefaults.EmbeddingApiFamily,
            new ModelId("text-embedding-004"),
            deploymentId: null,
            providerRequestId);

    [Fact]
    public async Task ParseAsync_WhenSingleEmbedding_DecodesDenseFloatVector()
    {
        var parser = new GoogleGeminiEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response.json"));
        var result = await parser.ParseAsync(body, CreateContext(new EmbeddingRequestId(Guid.NewGuid())), inputs, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        completed.Response.Items.Length.ShouldBe(1);
        completed.Response.Usage.ShouldBeSameAs(ModelUsage.NotReported);
        completed.Response.Usage.ReportState.ShouldBe(ModelUsageReportState.NotReported);

        var vector = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>().Vector.ShouldBeOfType<DenseFloatVector>();
        vector.Values.ShouldBe([0.1f, 0.2f, 0.3f]);
    }

    [Fact]
    public async Task ParseAsync_WhenBatchResponse_MapsByPositionInRequestOrder()
    {
        var parser = new GoogleGeminiEmbeddingResponseParser();
        var firstId = new EmbeddingInputId(Guid.NewGuid());
        var secondId = new EmbeddingInputId(Guid.NewGuid());
        var inputs = ImmutableArray.Create<EmbeddingInput>(
            new TextEmbeddingInput("first", firstId),
            new TextEmbeddingInput("second", secondId));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response_batch.json"));
        var result = await parser.ParseAsync(body, CreateContext(new EmbeddingRequestId(Guid.NewGuid())), inputs, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        completed.Response.Items.Length.ShouldBe(2);
        completed.Response.Usage.ShouldBeSameAs(ModelUsage.NotReported);
        completed.Response.Usage.ReportState.ShouldBe(ModelUsageReportState.NotReported);

        completed.Response.Items[0].InputIndex.ShouldBe(0);
        completed.Response.Items[0].CorrelationId.ShouldBe(firstId);
        completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>().Vector.ShouldBeOfType<DenseFloatVector>().Values.ShouldBe([0.1f, 0.2f]);

        completed.Response.Items[1].InputIndex.ShouldBe(1);
        completed.Response.Items[1].CorrelationId.ShouldBe(secondId);
        completed.Response.Items[1].ShouldBeOfType<EmbeddingItemSucceeded>().Vector.ShouldBeOfType<DenseFloatVector>().Values.ShouldBe([0.3f, 0.4f]);
    }

    [Fact]
    public async Task ParseAsync_WhenEmbeddingCountDoesNotMatchInputCount_FailsWithProtocolViolation()
    {
        var parser = new GoogleGeminiEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(
            new TextEmbeddingInput("first", null),
            new TextEmbeddingInput("second", null));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response.json"));
        var result = await parser.ParseAsync(body, CreateContext(new EmbeddingRequestId(Guid.NewGuid())), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Fact]
    public async Task ParseAsync_WhenBodyIsNotJson_FailsWithProtocolViolation()
    {
        var parser = new GoogleGeminiEmbeddingResponseParser();

        await using var body = new MemoryStream("not json"u8.ToArray());
        var result = await parser.ParseAsync(body, CreateContext(new EmbeddingRequestId(Guid.NewGuid())), [], TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<JsonException>();
    }

    [Fact]
    public async Task ParseAsync_WhenNoEmbeddings_FailsWithProtocolViolation()
    {
        var parser = new GoogleGeminiEmbeddingResponseParser();

        await using var body = new MemoryStream(/*lang=json,strict*/ """{"embeddings":[]}"""u8.ToArray());
        var result = await parser.ParseAsync(body, CreateContext(new EmbeddingRequestId(Guid.NewGuid())), [], TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Fact]
    public async Task ParseAsync_WhenSucceeding_ReportsProviderRequestIdOnResponseAndIdentity()
    {
        var providerRequestId = new ProviderRequestId("req_gemini_123");
        var parser = new GoogleGeminiEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response.json"));
        var result = await parser.ParseAsync(
            body, CreateContext(new EmbeddingRequestId(Guid.NewGuid()), providerRequestId), inputs, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        completed.Response.ProviderRequestId.ShouldBe(providerRequestId);

        var succeeded = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>();
        succeeded.Space.Provider.RequestId.ShouldBe(providerRequestId);
    }

    [Fact]
    public async Task ParseAsync_WhenFailing_ReportsProviderRequestIdOnFailure()
    {
        var providerRequestId = new ProviderRequestId("req_gemini_failure");
        var parser = new GoogleGeminiEmbeddingResponseParser();

        await using var body = new MemoryStream("not json"u8.ToArray());
        var result = await parser.ParseAsync(
            body, CreateContext(new EmbeddingRequestId(Guid.NewGuid()), providerRequestId), [], TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.RequestId.ShouldBe(providerRequestId);
    }

    [Fact]
    public async Task ParseAsync_WhenAnEmbeddingHasNoValues_ReturnsProtocolFailure()
    {
        var parser = new GoogleGeminiEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = new MemoryStream(
            /*lang=json,strict*/ """{"embeddings":[{"values":[]}]}"""u8.ToArray());

        var result = await parser.ParseAsync(body, CreateContext(new EmbeddingRequestId(Guid.NewGuid())), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider returned an empty embedding vector at position 0.");
    }
}
