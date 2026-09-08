// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI.Tests.Parsing;

/// <summary>
/// Verifies <see cref="GoogleVertexAIEmbeddingResponseParser.ParseAsync"/>
/// against fixture Vertex AI <c>:predict</c> response bodies.
/// </summary>
public sealed class GoogleVertexAIEmbeddingResponseParserTests
{
    private static GoogleVertexAIEmbeddingResponseParseContext CreateContext() =>
        new(
            new EmbeddingRequestId(Guid.NewGuid()),
            GoogleVertexAIProviderDefaults.ProviderId,
            GoogleVertexAIProviderDefaults.EmbeddingApiFamily,
            new ModelId("text-embedding-005"),
            deploymentId: null);

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
        completed.Response.Usage.InputTokens.ShouldBe(7);

        completed.Response.Items[0].CorrelationId.ShouldBe(firstId);
        completed.Response.Items[1].CorrelationId.ShouldBe(secondId);
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
}
