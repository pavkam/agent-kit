// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Web.Tests;

public sealed class WebFetchToolTests
{
    [Theory]
    [InlineData(/*lang=json,strict*/ "{}")]
    [InlineData(/*lang=json,strict*/ "{\"url\":\"file:///etc/passwd\"}")]
    [InlineData(/*lang=json,strict*/ "{\"url\":\"https://user:secret@example.test/\"}")]
    [InlineData(/*lang=json,strict*/ "{\"url\":\"https://example.test/#fragment\"}")]
    [InlineData(/*lang=json,strict*/ "{\"url\":\"https://example.test/\",\"maximum_characters\":0}")]
    public async Task InvokeAsync_WhenArgumentsInvalid_PerformsNoAuthorizationOrNetwork(string json)
    {
        var fixture = new Fixture();

        var result = await fixture.Tool.InvokeAsync(Request(json), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        fixture.Authority.Requests.ShouldBeEmpty();
        fixture.Resolver.Traces.ShouldBeEmpty();
        fixture.Transport.Traces.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenResolutionAuthorityDenies_PerformsNoNetworkPhase()
    {
        var fixture = new Fixture { Authority = { DenyAtRequest = 1 } };

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        fixture.Authority.Requests.Count.ShouldBe(1);
        fixture.Resolver.Traces.ShouldBeEmpty();
        fixture.Transport.Traces.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenHtmlResponseAuthorized_UsesExactTwoPhaseEvidenceAndProjectsUntrustedText()
    {
        var fixture = new Fixture();
        fixture.ScriptSuccess(
            fixture.Origin,
            "<html><style>hidden</style><body><h1>Hello</h1><script>steal()</script><p>World &amp; all</p></body></html>",
            "text/html; charset=utf-8");

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/","maximum_characters":100}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        fixture.Authority.Requests.Count.ShouldBe(2);
        fixture.Store.Enforcements.Count.ShouldBe(2);
        fixture.Resolver.Traces.Count.ShouldBe(1);
        fixture.Transport.Traces.Count.ShouldBe(1);
        using var json = ResultJson(result);
        json.RootElement.GetProperty("remote_content_trusted").GetBoolean().ShouldBeFalse();
        json.RootElement.GetProperty("transform").GetString().ShouldBe("html_to_text_v1");
        var content = json.RootElement.GetProperty("content").GetString()!;
        content.ShouldContain("Hello");
        content.ShouldContain("World & all");
        content.ShouldNotContain("hidden");
        content.ShouldNotContain("steal");
    }

    [Fact]
    public async Task InvokeAsync_WhenCrossOriginRedirected_ObtainsFreshResolutionAndSendGrants()
    {
        var fixture = new Fixture();
        var redirected = Destination("other.test", "/final");
        fixture.Resolver.Script(fixture.Origin, new NetworkResolved([Address()]));
        fixture.Transport.Script(fixture.Origin, new NetworkRedirectReceived(redirected, crossOrigin: true));
        fixture.ScriptSuccess(redirected, "done", "text/plain; charset=utf-8");

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        fixture.Authority.Requests.Count.ShouldBe(4);
        fixture.Store.Enforcements.Count.ShouldBe(4);
        fixture.Resolver.Traces.Select(static trace => trace.Destination.Host.Value)
            .ShouldBe(["example.test", "other.test"]);
        using var json = ResultJson(result);
        json.RootElement.GetProperty("final_url").GetString()!.ShouldContain("other.test");
        json.RootElement.GetProperty("redirects").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task InvokeAsync_WhenSendAuthorityDenies_PerformsResolutionButNoSend()
    {
        var fixture = new Fixture { Authority = { DenyAtRequest = 2 } };
        fixture.Resolver.Script(fixture.Origin, new NetworkResolved([Address()]));

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        fixture.Resolver.Traces.Count.ShouldBe(1);
        fixture.Transport.Traces.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenProjectionLimitReached_ReportsExplicitTruncation()
    {
        var fixture = new Fixture();
        fixture.ScriptSuccess(fixture.Origin, "abcdefghij", "text/plain");

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/","maximum_characters":4}"""),
            TestContext.Current.CancellationToken);

        using var json = ResultJson(result);
        json.RootElement.GetProperty("truncated").GetBoolean().ShouldBeTrue();
        json.RootElement.GetProperty("content").GetString().ShouldBe("abcd");
    }

    [Fact]
    public async Task InvokeAsync_WhenContentEncodingCompressed_RejectsBeforeReadingBody()
    {
        var fixture = new Fixture();
        fixture.Resolver.Script(fixture.Origin, new NetworkResolved([Address()]));
        fixture.Transport.Script(
            fixture.Origin,
            Response("body", "text/plain", 200, new NetworkHeader("Content-Encoding", "gzip")));

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.FailureReason!.ShouldContain("Compressed");
    }

    [Fact]
    public async Task InvokeAsync_WhenHttpError_PreservesBoundedUntrustedResponseEvidence()
    {
        var fixture = new Fixture();
        fixture.Resolver.Script(fixture.Origin, new NetworkResolved([Address()]));
        fixture.Transport.Script(fixture.Origin, Response("missing", "text/plain", 404));

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        using var json = ResultJson(result);
        json.RootElement.GetProperty("status").GetInt32().ShouldBe(404);
        json.RootElement.GetProperty("content").GetString().ShouldBe("missing");
        json.RootElement.GetProperty("remote_content_trusted").GetBoolean().ShouldBeFalse();
    }

    private static NetworkDestination Destination(string host, string route) => new(
        "https", new NormalizedHost(host), 443, new NetworkRoute(route));

    private static NetworkAddress Address() => new(
        IPAddress.Parse("192.0.2.1"), DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue);

    private static NetworkResponseReceived Response(
        string body,
        string mediaType,
        int status,
        params NetworkHeader[] additionalHeaders)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        ImmutableArray<NetworkHeader> headers =
            [new NetworkHeader("Content-Type", mediaType), .. additionalHeaders];
        return new NetworkResponseReceived(
            new ScriptedNetworkResponse(
                new NetworkResponseMetadata(status, new NetworkHeaderSet(headers), bytes.Length),
                bytes));
    }

    private static JsonDocument ResultJson(ToolInvocationResult result) =>
        JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);

    private static ToolInvocationRequest Request(string json) => new(
        TestSupport.TestSecurityEvidence.ToolContext(
            new AgentId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
            new SessionId(Guid.Parse("50000000-0000-0000-0000-000000000005")),
            new ToolCallId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
            new InRunOperationCorrelation(
                new OperationId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
                new RunId(Guid.Parse("80000000-0000-0000-0000-000000000008")),
                null),
            TestSupport.TestExecutionIdentity.Create(
                new TenantId("tenant"),
                new PrincipalId("principal"),
                ExecutionSubjectKind.Human)),
        JsonDocument.Parse(json).RootElement,
        DateTimeOffset.UnixEpoch);

    private sealed class Fixture
    {
        internal Fixture()
        {
            Store = new StrictGrantStore();
            Authority = new RecordingSecurityAuthority(Store);
            Resolver = new ScriptedNetworkNameResolver(Store, new FixedTimeProvider());
            Transport = new ScriptedNetworkTransport(Store, new FixedTimeProvider());
            Tool = new WebFetchTool(
                Resolver,
                Transport,
                Authority,
                new SequenceSecurityRequestIdGenerator(),
                new SequenceNetworkOperationIdGenerator(),
                new FixedTimeProvider(),
                Options.Create(new WebFetchToolOptions()));
        }

        internal NetworkDestination Origin { get; } = Destination("example.test", "/");
        internal StrictGrantStore Store { get; }
        internal RecordingSecurityAuthority Authority { get; }
        internal ScriptedNetworkNameResolver Resolver { get; }
        internal ScriptedNetworkTransport Transport { get; }
        internal WebFetchTool Tool { get; }

        internal void ScriptSuccess(NetworkDestination destination, string body, string mediaType)
        {
            Resolver.Script(destination, new NetworkResolved([Address()]));
            Transport.Script(destination, Response(body, mediaType, 200));
        }
    }
}
