// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Tests.Parsing;

/// <summary>Verifies <see cref="CohereRerankResponseParser"/> behavior.</summary>
public sealed class CohereRerankResponseParserTests
{
    [Fact]
    public async Task ParseAsync_WhenSucceeding_ReturnsOrderedResultsWithDocumentIds()
    {
        var parser = new CohereRerankResponseParser();
        var firstId = new DocumentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var secondId = new DocumentId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var documents = ImmutableArray.Create(
            new RerankDocument(firstId, 0, "first", ExtensionData.Empty),
            new RerankDocument(secondId, 1, "second", ExtensionData.Empty));

        await using var body = File.OpenRead(TestResources.GetPath("responses/rerank_response.json"));
        var result = await parser.ParseAsync(body, documents, CohereProviderDefaults.ProviderId, TestContext.Current.CancellationToken);

        var succeeded = result.ShouldBeOfType<RerankModelSucceeded>();
        succeeded.Response.Results.Length.ShouldBe(2);
        succeeded.Response.Results[0].InputIndex.ShouldBe(1);
        succeeded.Response.Results[0].DocumentId.ShouldBe(secondId);
        succeeded.Response.Results[1].InputIndex.ShouldBe(0);
        succeeded.Response.Usage.InputTokens.ShouldBe(2);
    }
}
