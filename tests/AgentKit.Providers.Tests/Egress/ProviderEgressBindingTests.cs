// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests.Egress;

using System.Net.Http;

using AgentKit.Providers.Egress;
using AgentKit.TestSupport;

/// <summary>Verifies the secret-free resource, fingerprint, and enforcement evidence <see cref="ProviderEgress"/> binds.</summary>
public sealed class ProviderEgressBindingTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static NetworkDestination Destination(string route) =>
        new("https", new NormalizedHost("api.provider.test"), 443, new NetworkRoute(route));

    private static ProviderEgressRequest Request(HttpRequestMessage message) => new(
        ProviderEgressHarness.Operation,
        ProviderEgressOperation.Conversation,
        new ProviderId("openai"),
        new ApiFamilyId("openai-chat"),
        new ProviderServiceSurfaceId("chat"),
        new ProviderEndpointId("default"),
        binding: null,
        new ModelId("gpt-test"),
        deploymentId: null,
        modelRevision: null,
        attempt: 1,
        Now.AddMinutes(1),
        streaming: false,
        message);

    [Fact]
    public void Resources_WhenRouteHasQuery_ReplacesTheQueryWithItsFingerprint()
    {
        var resources = ProviderEgressBinding.Resources(Destination("/v1/chat?key=super-secret"));

        var resource = resources.ShouldHaveSingleItem();
        resource.Identifier.ShouldStartWith("https://api.provider.test:443/v1/chat?query=");
        resource.Identifier.ShouldNotContain("super-secret");
    }

    [Fact]
    public void Resources_WhenRouteHasNoQuery_UsesTheCanonicalDestination()
    {
        var resource = ProviderEgressBinding.Resources(Destination("/v1/chat")).ShouldHaveSingleItem();

        resource.Identifier.ShouldBe("https://api.provider.test:443/v1/chat");
    }

    [Fact]
    public void Fingerprint_WhenHeaderValuesDifferButNamesMatch_IsIdentical()
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.provider.test/v1/chat");
        var request = Request(message);
        var destination = Destination("/v1/chat");
        var first = new NetworkHeaderSet([new NetworkHeader("Authorization", "Bearer one")]);
        var second = new NetworkHeaderSet([new NetworkHeader("authorization", "Bearer two")]);

        var a = ProviderEgressBinding.Fingerprint(request, NetworkMethod.Post, destination, first, null, NetworkDataClassification.Confidential, 10);
        var b = ProviderEgressBinding.Fingerprint(request, NetworkMethod.Post, destination, second, null, NetworkDataClassification.Confidential, 10);

        a.ShouldBe(b);
        a.Value.ShouldStartWith("sha256:");
    }

    [Fact]
    public void Fingerprint_WhenAHeaderNameIsAdded_Differs()
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.provider.test/v1/chat");
        var request = Request(message);
        var destination = Destination("/v1/chat");
        var baseline = ProviderEgressBinding.Fingerprint(request, NetworkMethod.Post, destination, NetworkHeaderSet.Empty, null, NetworkDataClassification.Confidential, 10);
        var added = ProviderEgressBinding.Fingerprint(
            request,
            NetworkMethod.Post,
            destination,
            new NetworkHeaderSet([new NetworkHeader("x-extra", "1")]),
            null,
            NetworkDataClassification.Confidential,
            10);

        added.ShouldNotBe(baseline);
    }

    [Fact]
    public void Fingerprint_WhenResponseBoundOrClassificationChanges_Differs()
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.provider.test/v1/chat");
        var request = Request(message);
        var destination = Destination("/v1/chat");
        var baseline = ProviderEgressBinding.Fingerprint(request, NetworkMethod.Post, destination, NetworkHeaderSet.Empty, null, NetworkDataClassification.Confidential, 10);

        ProviderEgressBinding.Fingerprint(request, NetworkMethod.Post, destination, NetworkHeaderSet.Empty, null, NetworkDataClassification.Confidential, 11)
            .ShouldNotBe(baseline);
        ProviderEgressBinding.Fingerprint(request, NetworkMethod.Post, destination, NetworkHeaderSet.Empty, null, NetworkDataClassification.Public, 10)
            .ShouldNotBe(baseline);
    }

    [Fact]
    public void DenialMessage_WhenGrantWasNotConsumed_ReturnsTheStoreMessage()
    {
        var exhausted = new GrantConsumptionResult(GrantConsumptionStatus.Exhausted, 0, "Already used.", null);

        ProviderEgressBinding.DenialMessage(exhausted).ShouldBe("Already used.");
    }

    [Fact]
    public void Audience_IsDistinctFromTheNetworkBoundaryAudiences()
    {
        ProviderEgress.SecurityAudience.ShouldBe(new ComponentId("agentkit.providers.egress"));
        ProviderEgress.SecurityAudience.ShouldNotBe(new ComponentId("agentkit.network.transport"));
    }
}
