// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI.Tests.Parsing;

using System.Text;

/// <summary>
/// Verifies <see cref="GoogleVertexAIEmbeddingResponseParser.ParseAsync"/>
/// against fixture Vertex AI <c>:predict</c> response bodies.
/// </summary>
public sealed class GoogleVertexAIEmbeddingResponseParserTests
{
    private static EmbeddingResponseParseContext CreateContext(ProviderRequestId? providerRequestId = null) =>
        new(
            new EmbeddingRequestId(Guid.NewGuid()),
            GoogleVertexAIProviderDefaults.ProviderId,
            GoogleVertexAIProviderDefaults.EmbeddingApiFamily,
            new ModelId("text-embedding-005"),
            deploymentId: null,
            providerRequestId);

    [Fact]
    public async Task ParseAsync_WhenSinglePrediction_DecodesVectorAndUsage()
    {
        var parser = new GoogleVertexAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response.json"));
        var result = await parser.ParseAsync(body, CreateContext(), inputs, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        var item = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>();
        item.Vector.ShouldBeOfType<DenseFloatVector>().Values.ShouldBe([0.1f, 0.2f, 0.3f]);

        completed.Response.Usage.ReportState.ShouldBe(ModelUsageReportState.Final);
        completed.Response.Usage.InputTokens.ShouldBe(6);
    }

    [Fact]
    public async Task ParseAsync_WhenBatchResponse_MapsByPositionAndSumsTokenCounts()
    {
        var parser = new GoogleVertexAIEmbeddingResponseParser();
        var firstId = new EmbeddingInputId(Guid.NewGuid());
        var secondId = new EmbeddingInputId(Guid.NewGuid());
        var inputs = ImmutableArray.Create<EmbeddingInput>(
            new TextEmbeddingInput("first", firstId),
            new TextEmbeddingInput("second", secondId));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response_batch.json"));
        var result = await parser.ParseAsync(body, CreateContext(), inputs, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        completed.Response.Items.Length.ShouldBe(2);
        completed.Response.Usage.ReportState.ShouldBe(ModelUsageReportState.Final);
        completed.Response.Usage.InputTokens.ShouldBe(7);

        completed.Response.Items[0].CorrelationId.ShouldBe(firstId);
        completed.Response.Items[1].CorrelationId.ShouldBe(secondId);
    }

    [Fact]
    public async Task ParseAsync_WhenUsageTokenCountIsNegative_FailsWithProtocolViolation()
    {
        var parser = new GoogleVertexAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));
        var payload = TestResources.ReadAllText("responses/embedding_response.json")
            .Replace("\"token_count\": 6", "\"token_count\": -1", StringComparison.Ordinal);

        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        var result = await parser.ParseAsync(body, CreateContext(), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Theory]
    [InlineData(-1L, 5L)]
    [InlineData(long.MaxValue, 1L)]
    public async Task ParseAsync_WhenBatchUsageIsNegativeOrOverflows_FailsWithProtocolViolation(
        long firstCount,
        long secondCount)
    {
        var parser = new GoogleVertexAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(
            new TextEmbeddingInput("first", null),
            new TextEmbeddingInput("second", null));
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            predictions = new[] { firstCount, secondCount }.Select(count => new
            {
                embeddings = new { values = ImmutableArray.Create(0.1f), statistics = new { token_count = count } },
            }),
        });

        await using var body = new MemoryStream(payload);
        var result = await parser.ParseAsync(body, CreateContext(), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        if (firstCount < 0)
        {
            failed.Failure.DiagnosticCause.ShouldBeOfType<ArgumentOutOfRangeException>().ParamName.ShouldBe("tokenCount");
        }
        else
        {
            _ = failed.Failure.DiagnosticCause.ShouldBeOfType<OverflowException>();
        }
    }

    [Fact]
    public async Task ParseAsync_WhenEmbeddingCountDoesNotMatchInputCount_FailsWithProtocolViolation()
    {
        var parser = new GoogleVertexAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(
            new TextEmbeddingInput("first", null),
            new TextEmbeddingInput("second", null));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response.json"));
        var result = await parser.ParseAsync(body, CreateContext(), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Fact]
    public async Task ParseAsync_WhenBodyIsNotJson_FailsWithProtocolViolation()
    {
        var parser = new GoogleVertexAIEmbeddingResponseParser();

        await using var body = new MemoryStream("not json"u8.ToArray());
        var result = await parser.ParseAsync(body, CreateContext(), [], TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<JsonException>();
    }

    [Fact]
    public async Task ParseAsync_WhenNoPredictions_FailsWithProtocolViolation()
    {
        var parser = new GoogleVertexAIEmbeddingResponseParser();

        await using var body = new MemoryStream(/*lang=json,strict*/ """{"predictions":[]}"""u8.ToArray());
        var result = await parser.ParseAsync(body, CreateContext(), [], TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Fact]
    public async Task ParseAsync_WhenSucceeding_ReportsProviderRequestIdOnResponseAndIdentity()
    {
        var providerRequestId = new ProviderRequestId("req_vertex_123");
        var parser = new GoogleVertexAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = File.OpenRead(TestResources.GetPath("responses/embedding_response.json"));
        var result = await parser.ParseAsync(body, CreateContext(providerRequestId), inputs, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        completed.Response.ProviderRequestId.ShouldBe(providerRequestId);

        var succeeded = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>();
        succeeded.Space.Provider.RequestId.ShouldBe(providerRequestId);
    }

    [Fact]
    public async Task ParseAsync_WhenFailing_ReportsProviderRequestIdOnFailure()
    {
        var providerRequestId = new ProviderRequestId("req_vertex_failure");
        var parser = new GoogleVertexAIEmbeddingResponseParser();

        await using var body = new MemoryStream("not json"u8.ToArray());
        var result = await parser.ParseAsync(body, CreateContext(providerRequestId), [], TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.RequestId.ShouldBe(providerRequestId);
    }

    [Fact]
    public async Task ParseAsync_WhenAPredictionHasNoValues_ReturnsProtocolFailure()
    {
        var parser = new GoogleVertexAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = new MemoryStream(
            /*lang=json,strict*/ """{"predictions":[{"embeddings":{"values":[]}}]}"""u8.ToArray());

        var result = await parser.ParseAsync(body, CreateContext(), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider returned an empty embedding vector at position 0.");
    }

    [Fact]
    public async Task ParseAsync_WhenPredictionsFieldIsAbsent_FailsWithProtocolViolation()
    {
        var parser = new GoogleVertexAIEmbeddingResponseParser();

        await using var body = new MemoryStream(/*lang=json,strict*/ """{}"""u8.ToArray());
        var result = await parser.ParseAsync(body, CreateContext(), [], TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider returned a predict response with no predictions.");
    }

    [Fact]
    public async Task ParseAsync_WhenAPredictionHasNoEmbeddingsObject_ReturnsProtocolFailure()
    {
        var parser = new GoogleVertexAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = new MemoryStream(/*lang=json,strict*/ """{"predictions":[{}]}"""u8.ToArray());
        var result = await parser.ParseAsync(body, CreateContext(), inputs, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider returned an empty embedding vector at position 0.");
    }

    [Fact]
    public async Task ParseAsync_WhenStatisticsAreAbsent_ReportsUsageNotReported()
    {
        var parser = new GoogleVertexAIEmbeddingResponseParser();
        var inputs = ImmutableArray.Create<EmbeddingInput>(new TextEmbeddingInput("hello", null));

        await using var body = new MemoryStream(
            /*lang=json,strict*/ """{"predictions":[{"embeddings":{"values":[0.1]}}]}"""u8.ToArray());
        var result = await parser.ParseAsync(body, CreateContext(), inputs, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        completed.Response.Usage.ShouldBe(ModelUsage.NotReported);

        var item = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>();
        item.Extensions.Values.ContainsKey("truncated").ShouldBeFalse();
    }
}
