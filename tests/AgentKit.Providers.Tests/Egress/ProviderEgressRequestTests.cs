// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests.Egress;

using System.Net.Http;

using AgentKit.Providers.Egress;
using AgentKit.TestSupport;

/// <summary>Verifies <see cref="ProviderEgressRequest"/> validation and its per-operation factories.</summary>
public sealed class ProviderEgressRequestTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly ProviderOperationBinding Binding = new(
        new ProviderEndpointProfileReference(new ProviderEndpointProfileKey("endpoint"), new ProviderEndpointProfileVersion(2)),
        new ProviderCredentialProfileReference(new ProviderCredentialProfileKey("credential"), new ProviderCredentialProfileVersion(3)));

    private static HttpRequestMessage Message() => new(HttpMethod.Post, "https://api.provider.test/v1/x");

    [Fact]
    public void ForConversation_WhenDescriptorIsBound_CopiesIdentityBindingAttemptAndDeadline()
    {
        var descriptor = ProviderTestData.Model("chat") with
        {
            Binding = Binding,
            ServiceSurface = new ProviderServiceSurfaceId("chat"),
            EndpointId = new ProviderEndpointId("default"),
        };
        var context = new LlmRequestContext(
            ProviderTestData.ModelRequestId,
            descriptor,
            [],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, 2, Now, ProviderRequestOptions.Empty, ProviderEgressHarness.Operation);
        using var message = Message();

        var egress = ProviderEgressRequest.ForConversation(descriptor, request, message, streaming: true);

        egress.Kind.ShouldBe(ProviderEgressOperation.Conversation);
        egress.Operation.ShouldBe(ProviderEgressHarness.Operation);
        egress.ProviderId.ShouldBe(descriptor.ProviderId);
        egress.Binding.ShouldBe(Binding);
        egress.ModelId.ShouldBe(descriptor.ModelId);
        egress.Attempt.ShouldBe(2);
        egress.Deadline.ShouldBe(Now);
        egress.Streaming.ShouldBeTrue();
        egress.Message.ShouldBeSameAs(message);
    }

    [Fact]
    public void ForEmbedding_WhenCalled_MarksTheEmbeddingOperationAsNonStreaming()
    {
        var descriptor = ProviderTestData.EmbeddingModel("embed") with { Binding = Binding };
        var context = new EmbeddingRequestContext(
            new EmbeddingRequestId(Guid.NewGuid()),
            descriptor,
            new EmbeddingRequest([new TextEmbeddingInput("hi", null)], EmbeddingPurpose.Unspecified, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty));
        var request = new EmbeddingModelRequest(context, 1, Now, ProviderRequestOptions.Empty) { Operation = ProviderEgressHarness.Operation };
        using var message = Message();

        var egress = ProviderEgressRequest.ForEmbedding(descriptor, request, message);

        egress.Kind.ShouldBe(ProviderEgressOperation.Embedding);
        egress.Binding.ShouldBe(Binding);
        egress.Streaming.ShouldBeFalse();
        egress.Operation.ShouldBe(ProviderEgressHarness.Operation);
    }

    [Fact]
    public void ForReranking_WhenCalled_MarksTheRerankingOperationAndCarriesTheRequiredOperation()
    {
        var unbound = new RerankerDescriptor(
            new RerankerAlias("rank"),
            new ProviderId("cohere"),
            new ApiFamilyId("cohere-rerank"),
            new ModelId("rerank"),
            deploymentId: null,
            new RerankerCapabilities(supportsTopCount: false, ExtensionData.Empty),
            new RerankerLimits(null, null),
            ExtensionData.Empty);
        var descriptor = unbound with { Binding = Binding };
        var request = new RerankModelRequest(
            ProviderEgressHarness.Operation,
            new RerankerSelectionDecision(descriptor, "test", new ModelCatalogVersion(1)),
            new RerankRequest("q", [new RerankDocument(new DocumentId(Guid.NewGuid()), 0, "d", ExtensionData.Empty)], null, ProviderRequestOptions.Empty),
            3,
            Now);
        using var message = Message();

        var egress = ProviderEgressRequest.ForReranking(descriptor, request, message);

        egress.Kind.ShouldBe(ProviderEgressOperation.Reranking);
        egress.Attempt.ShouldBe(3);
        egress.Binding.ShouldBe(Binding);
        egress.Operation.ShouldBe(ProviderEgressHarness.Operation);
    }

    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        using var message = Message();

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ProviderEgressRequest(
            null,
            (ProviderEgressOperation) 99,
            new ProviderId("p"),
            new ApiFamilyId("f"),
            new ProviderServiceSurfaceId("s"),
            new ProviderEndpointId("e"),
            null,
            new ModelId("m"),
            null,
            null,
            1,
            Now,
            false,
            message));

        exception.ParamName.ShouldBe("kind");
    }

    [Fact]
    public void Constructor_WhenMessageIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ProviderEgressRequest(
            null,
            ProviderEgressOperation.Conversation,
            new ProviderId("p"),
            new ApiFamilyId("f"),
            new ProviderServiceSurfaceId("s"),
            new ProviderEndpointId("e"),
            null,
            new ModelId("m"),
            null,
            null,
            1,
            Now,
            false,
            null!));

        exception.ParamName.ShouldBe("message");
    }

    [Fact]
    public void ForConversation_WhenAnyArgumentIsNull_ThrowsArgumentNullException()
    {
        using var message = Message();
        var descriptor = ProviderTestData.Model("chat");

        Should.Throw<ArgumentNullException>(() => ProviderEgressRequest.ForConversation(null!, null!, message, false)).ParamName.ShouldBe("descriptor");
        Should.Throw<ArgumentNullException>(() => ProviderEgressRequest.ForConversation(descriptor, null!, message, false)).ParamName.ShouldBe("request");
    }
}
