// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenRouter.Tests;

using System.Net;
using System.Net.Http;

using AgentKit.Providers.Credentials;
using AgentKit.Providers.Egress;
using AgentKit.Providers.OpenRouter.Tests.Fakes;
using AgentKit.TestSupport;

/// <summary>Verifies <see cref="OpenRouterReranker"/> sends through provider egress and maps its outcomes.</summary>
public sealed class OpenRouterRerankerTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly RerankerDescriptor Descriptor = ProviderEgressHarness.Bind(new RerankerDescriptor(
        new RerankerAlias("rank"),
        OpenRouterProviderDefaults.ProviderId,
        OpenRouterProviderDefaults.ApiFamily,
        new ModelId("cohere/rerank-v3.5"),
        deploymentId: null,
        new RerankerCapabilities(supportsTopCount: true, ExtensionData.Empty),
        new RerankerLimits(maxDocumentsPerRequest: 100, maxDocumentCharacters: 4096),
        ExtensionData.Empty));

    private static RerankModelRequest CreateRequest(ProtectedSemanticOperationContext? operation = null) => new(
        operation ?? ProviderEgressHarness.Operation,
        new RerankerSelectionDecision(Descriptor, "test", new ModelCatalogVersion(1)),
        new RerankRequest(
            "which is best",
            [
                new RerankDocument(new DocumentId(Guid.NewGuid()), 0, "alpha", ExtensionData.Empty),
                new RerankDocument(new DocumentId(Guid.NewGuid()), 1, "beta", ExtensionData.Empty),
            ],
            topCount: 2,
            ProviderRequestOptions.Empty),
        attempt: 1,
        Now.AddMinutes(1));

    private static OpenRouterReranker CreateReranker(StubHttpMessageHandler handler, ProviderEgressHarness? harness = null) => new(
        Descriptor,
        new OpenRouterProviderOptions(),
        new OpenRouterRerankRequestTranslator(),
        new OpenRouterRerankResponseParser(),
        (harness ?? ProviderEgressHarness.Create(handler, new FakeTimeProvider(Now))).Egress,
        new FakeTimeProvider(Now), new StaticProviderProfileRuntimeSelector(new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-or-rerank-secret")), OpenRouterProviderDefaults.DefaultBaseAddress));

    private static StubHttpMessageHandler Serving(string json) => new(_ => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
    });

    [Fact]
    public async Task RerankAsync_WhenAdmitted_SendsThroughTheNetworkBoundaryAndParsesResults()
    {
        var handler = Serving(/*lang=json,strict*/ """{"results":[{"index":1,"relevance_score":0.9},{"index":0,"relevance_score":0.1}]}""");
        var harness = ProviderEgressHarness.Create(handler, new FakeTimeProvider(Now));

        var result = await CreateReranker(handler, harness).RerankAsync(CreateRequest(), TestContext.Current.CancellationToken);

        var response = result.ShouldBeOfType<RerankModelSucceeded>().Response;
        response.Results.Select(static item => item.InputIndex).ShouldBe([1, 0]);
        var sent = handler.Requests.ShouldHaveSingleItem();
        sent.RequestUri.ShouldBe(new Uri("https://openrouter.ai/api/v1/rerank"));
        sent.Headers.Authorization!.Parameter.ShouldBe("sk-or-rerank-secret");
        harness.Authority.Requests.Select(static request => request.Audience).ShouldBe(
        [
            ProviderCredentialReadGate.DefaultAudience,
            ProviderEgress.SecurityAudience,
            harness.Resolver.SecurityAudience,
            harness.Transport.SecurityAudience,
        ]);
        harness.Authority.Requests[1].Resources.ShouldHaveSingleItem().Identifier.ShouldBe("https://openrouter.ai:443/api/v1/rerank");
    }

    [Fact]
    public async Task RerankAsync_WhenEgressGrantDenied_FailsAuthorizationWithoutAnyIo()
    {
        var handler = Serving(/*lang=json,strict*/ """{"results":[]}""");
        var harness = ProviderEgressHarness.Create(handler, new FakeTimeProvider(Now));
        harness.Authority.Deny = static request => request.Audience == ProviderEgress.SecurityAudience;

        var result = await CreateReranker(handler, harness).RerankAsync(CreateRequest(), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<RerankModelFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Authorization);
        failed.Failure.SafeMessage.ShouldNotContain("sk-or-rerank-secret");
        handler.Requests.ShouldBeEmpty();
        harness.Resolver.Resolved.ShouldBeEmpty();
    }

    [Fact]
    public async Task RerankAsync_WhenRequiredAuditIsUnavailable_FailsClosedWithoutSending()
    {
        var handler = Serving(/*lang=json,strict*/ """{"results":[]}""");
        var harness = ProviderEgressHarness.Create(handler, new FakeTimeProvider(Now));
        harness.Audit.Result = new SecurityAuditFailed("sink failed");

        var result = await CreateReranker(handler, harness).RerankAsync(CreateRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<RerankModelFailed>().Failure.Kind.ShouldBe(ProviderFailureKind.Authorization);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RerankAsync_WhenProviderRefusesTheConnection_ReturnsTypedUnavailableFailure()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("refused"));

        var result = await CreateReranker(handler).RerankAsync(CreateRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<RerankModelFailed>().Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
    }

    [Fact]
    public async Task RerankAsync_WhenProviderReturnsAnErrorStatus_ReturnsStatusMappedFailure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        var result = await CreateReranker(handler).RerankAsync(CreateRequest(), TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<RerankModelFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Throttling);
        failure.StatusCode.ShouldBe(429);
    }

    [Fact]
    public async Task RerankAsync_WhenCallerAlreadyCancelled_FailsAsCancellationWithoutSending()
    {
        var handler = Serving(/*lang=json,strict*/ """{"results":[]}""");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await CreateReranker(handler).RerankAsync(CreateRequest(), cancellation.Token);

        result.ShouldBeOfType<RerankModelFailed>().Failure.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        handler.Requests.ShouldBeEmpty();
    }
}
