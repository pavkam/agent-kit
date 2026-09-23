// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.WebSearch.Tests;

using System.Net;
using System.Text;

/// <summary>Verifies <see cref="NetworkWebSearchProvider"/> grant binding and wire parsing.</summary>
public sealed class NetworkWebSearchProviderTests
{
    [Fact]
    public async Task SearchAsync_WhenResponseValid_ReturnsSucceededResults()
    {
        const string body = /*lang=json,strict*/ """
            {
              "complete": true,
              "results": [
                {
                  "title": "AgentKit",
                  "url": "https://example.com/docs",
                  "snippet": "Documentation.",
                  "published_at": "2024-01-02T03:04:05Z"
                }
              ]
            }
            """;
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
        var provider = Provider(handler);
        var request = AuthorizedRequest(provider, /*lang=json,strict*/ """{"query":"agentkit"}""");
        var result = await provider.SearchAsync(request, TestContext.Current.CancellationToken);
        result.ShouldBeOfType<WebSearchSucceeded>().Items.ShouldHaveSingleItem().Title.ShouldBe("AgentKit");
    }

    [Fact]
    public async Task SearchAsync_WhenGrantMismatches_ReturnsDenied()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var provider = Provider(handler);
        var request = AuthorizedRequest(provider, /*lang=json,strict*/ """{"query":"agentkit"}""");
        var tampered = request with
        {
            Grant = request.Grant with { InputFingerprint = new InputFingerprint("deadbeef") },
        };
        var result = await provider.SearchAsync(tampered, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<WebSearchDenied>();
    }

    private static NetworkWebSearchProvider Provider(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler),
            new NetworkWebSearchProviderOptions
            {
                Endpoint = new Uri("https://search.example.test/query"),
            },
            new FixedTimeProvider());

    private static WebSearchRequest AuthorizedRequest(NetworkWebSearchProvider provider, string json)
    {
        var context = TestData.Context;
        var arguments = JsonDocument.Parse(json).RootElement;
        var query = arguments.GetProperty("query").GetString()!;
        var searchId = new WebSearchRequestId(Guid.Parse("30000000-0000-0000-0000-000000000003"));
        var deadline = DateTimeOffset.UnixEpoch.AddMinutes(1);
        var fingerprint = WebSearchSecurityBinding.Fingerprint(
            searchId,
            provider.ProviderId,
            provider.Destination,
            query,
            [],
            WebSearchFreshness.Any,
            5,
            deadline);
        var grant = new SecurityGrant(
            new GrantId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            new SecurityRequestId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
            context.Authorization.Scope,
            context.Authorization.Identity,
            provider.SecurityAudience,
            SecurityOperationKind.Network,
            SecurityEffect.Egress,
            [provider.Destination],
            fingerprint,
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            DateTimeOffset.UnixEpoch,
            deadline,
            1);
        return new WebSearchRequest(searchId, context, query, [], WebSearchFreshness.Any, 5, deadline, grant);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond): HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
