// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Web.Tests;

using AgentKit.TestSupport;

public sealed class WebFetchToolTests
{
    [Theory]
    [InlineData(/*lang=json,strict*/ "{}")]
    [InlineData(/*lang=json,strict*/ "{\"url\":\"file:///etc/passwd\"}")]
    [InlineData(/*lang=json,strict*/ "{\"url\":\"https://user:secret@example.test/\"}")]
    [InlineData(/*lang=json,strict*/ "{\"url\":\"https://example.test/#fragment\"}")]
    [InlineData(/*lang=json,strict*/ "{\"url\":\"https://example.test/\",\"maximum_characters\":0}")]
    [InlineData(/*lang=json,strict*/ "{\"url\":\"https://[fe80::1%25eth0]/\"}")]
    public async Task InvokeAsync_WhenArgumentsInvalid_PerformsNoAuthorizationOrNetwork(string json)
    {
        var fixture = new Fixture();

        var result = await fixture.Tool.InvokeAsync(Request(json), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
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

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
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

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
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

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public async Task InvokeAsync_WhenAuthorityDeniesAfterRedirect_PreservesPriorEgress(int denialRequest)
    {
        var fixture = new Fixture { Authority = { DenyAtRequest = denialRequest } };
        var redirected = Destination("other.test", "/final");
        fixture.Resolver.Script(fixture.Origin, new NetworkResolved([Address()]));
        fixture.Transport.Script(fixture.Origin, new NetworkRedirectReceived(redirected, crossOrigin: true));
        fixture.Resolver.Script(redirected, new NetworkResolved([Address()]));

        var result = await fixture.Tool.InvokeAsync(Request(/*lang=json,strict*/ """{"url":"https://example.test/"}"""), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvocationFailed);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.PartiallyPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
        _ = fixture.Transport.Traces.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task InvokeAsync_WhenOverallDeadlineElapsesBeforeFirstResolution_ReturnsTimedOut()
    {
        var calls = 0;
        var clock = new CallbackUtcNowTimeProvider(() => calls++ == 0 ? DateTimeOffset.UnixEpoch : DateTimeOffset.UnixEpoch + TimeSpan.FromDays(1));
        var fixture = new Fixture(clock, new WebFetchToolOptions { DefaultTimeout = TimeSpan.FromSeconds(1) });

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.TimedOut);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        fixture.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenResolutionFails_ProjectsResolverFailureKind()
    {
        var fixture = new Fixture();
        fixture.Resolver.Script(fixture.Origin, new NetworkResolutionFailed(NetworkFailureKind.DnsResolutionFailed, "DNS lookup failed."));

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvocationFailed);
        result.Outcome.FailureReason.ShouldBe("DNS lookup failed.");
    }

    [Fact]
    public async Task InvokeAsync_WhenResolutionDenied_ReturnsDenied()
    {
        var fixture = new Fixture();
        fixture.Resolver.Script(fixture.Origin, new NetworkResolutionDenied("Destination is not permitted."));

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.Denied);
    }

    [Fact]
    public async Task InvokeAsync_WhenRedirectLimitExceeded_ReturnsRedirectLimitFailure()
    {
        var fixture = new Fixture(options: new WebFetchToolOptions { MaximumRedirects = 0 });
        fixture.Resolver.Script(fixture.Origin, new NetworkResolved([Address()]));
        fixture.Transport.Script(fixture.Origin, new NetworkRedirectReceived(Destination("other.test", "/final"), crossOrigin: true));

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.FailureReason!.ShouldContain("redirect boundary");
    }

    [Fact]
    public async Task InvokeAsync_WhenRedirectTargetsAnUnsupportedScheme_RejectsInsteadOfAdoptingItVerbatim()
    {
        // TryDestination rejects non-http(s) schemes, userinfo, and fragments for the initial URL, but
        // NetworkDestination itself accepts any non-blank scheme. A redirect destination is built by the
        // transport, not through TryDestination, so a Location: ftp://... (or file://...) redirect must not be
        // re-authorized and re-resolved without the same tool-level policy that gated the first hop.
        var fixture = new Fixture();
        fixture.Resolver.Script(fixture.Origin, new NetworkResolved([Address()]));
        var redirected = new NetworkDestination("ftp", new NormalizedHost("other.test"), 21, new NetworkRoute("/final"));
        fixture.Transport.Script(fixture.Origin, new NetworkRedirectReceived(redirected, crossOrigin: true));

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.Unsupported);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.PartiallyPerformed);
        result.Outcome.FailureReason!.ShouldContain("scheme");
        _ = fixture.Resolver.Traces.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(NetworkFailureKind.Timeout, ToolTerminalStatus.TimedOut, ToolCallOutcomeKind.Failed)]
    [InlineData(NetworkFailureKind.Cancelled, ToolTerminalStatus.Cancelled, ToolCallOutcomeKind.Cancelled)]
    [InlineData(NetworkFailureKind.UnsupportedScheme, ToolTerminalStatus.Unsupported, ToolCallOutcomeKind.Rejected)]
    [InlineData(NetworkFailureKind.ProtocolViolation, ToolTerminalStatus.ProtocolFailed, ToolCallOutcomeKind.Failed)]
    [InlineData(NetworkFailureKind.DnsResolutionFailed, ToolTerminalStatus.InvocationFailed, ToolCallOutcomeKind.Failed)]
    [InlineData(NetworkFailureKind.ConnectionFailed, ToolTerminalStatus.InvocationFailed, ToolCallOutcomeKind.Failed)]
    [InlineData(NetworkFailureKind.TlsFailure, ToolTerminalStatus.InvocationFailed, ToolCallOutcomeKind.Failed)]
    [InlineData(NetworkFailureKind.Unknown, ToolTerminalStatus.InvocationFailed, ToolCallOutcomeKind.Failed)]
    public async Task InvokeAsync_WhenSendFailsWithEachFailureKind_ProjectsTerminalStatus(NetworkFailureKind kind, ToolTerminalStatus expectedStatus, ToolCallOutcomeKind expectedKind)
    {
        var fixture = new Fixture();
        fixture.Resolver.Script(fixture.Origin, new NetworkResolved([Address()]));
        fixture.Transport.Script(fixture.Origin, new NetworkRequestFailed(kind, "Transport failure.", sideEffectCertain: true));

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(expectedKind);
        result.Outcome.SourceStatus.ShouldBe(expectedStatus);
    }

    [Fact]
    public async Task InvokeAsync_WhenSendDenied_ReturnsDenied()
    {
        var fixture = new Fixture();
        fixture.Resolver.Script(fixture.Origin, new NetworkResolved([Address()]));
        fixture.Transport.Script(fixture.Origin, new NetworkDenied("Egress is not permitted."));

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.Denied);
    }

    [Fact]
    public async Task InvokeAsync_WhenResponseLimitExceeded_ReturnsResponseLimitFailure()
    {
        var fixture = new Fixture();
        fixture.Resolver.Script(fixture.Origin, new NetworkResolved([Address()]));
        fixture.Transport.Script(fixture.Origin, new NetworkResponseLimitExceeded(10, 5));

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.FailureReason!.ShouldContain("byte boundary");
    }

    [Fact]
    public async Task InvokeAsync_WhenSendReportsRedirectLimitExceeded_ReturnsRedirectLimitFailure()
    {
        var fixture = new Fixture();
        fixture.Resolver.Script(fixture.Origin, new NetworkResolved([Address()]));
        fixture.Transport.Script(fixture.Origin, new NetworkRedirectLimitExceeded(5));

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.FailureReason!.ShouldContain("redirect boundary");
    }

    [Fact]
    public async Task InvokeAsync_WhenSendCancelled_ReturnsCancelledStatus()
    {
        var fixture = new Fixture();
        fixture.Resolver.Script(fixture.Origin, new NetworkResolved([Address()]));
        fixture.Transport.Script(fixture.Origin, new NetworkCancelled(sideEffectCertain: true));

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Cancelled);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.Cancelled);
    }

    [Fact]
    public async Task InvokeAsync_WhenResponseBodyTooLarge_ReturnsResponseLimitFailure()
    {
        var fixture = new Fixture();
        fixture.Resolver.Script(fixture.Origin, new NetworkResolved([Address()]));
        fixture.Transport.Script(fixture.Origin, new NetworkResponseReceived(
            new ThrowingNetworkResponse(new NetworkResponseTooLargeException(1, 2))));

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.ResultNormalizationFailed);
        result.Outcome.FailureReason!.ShouldContain("byte boundary");
    }

    [Fact]
    public async Task InvokeAsync_WhenResponseBodyTimesOut_ReturnsTimedOut()
    {
        var fixture = new Fixture();
        fixture.Resolver.Script(fixture.Origin, new NetworkResolved([Address()]));
        fixture.Transport.Script(fixture.Origin, new NetworkResponseReceived(
            new ThrowingNetworkResponse(new NetworkResponseTimedOutException())));

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.TimedOut);
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderCountExceedsBound_ReturnsHeaderLimitFailure()
    {
        var fixture = new Fixture(options: new WebFetchToolOptions { MaximumHeaderCount = 1 });
        fixture.Resolver.Script(fixture.Origin, new NetworkResolved([Address()]));
        fixture.Transport.Script(fixture.Origin, Response("ok", "text/plain", 200, new NetworkHeader("X-Extra", "value")));

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.FailureReason!.ShouldContain("headers exceeded");
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderCharactersExceedBound_ReturnsHeaderLimitFailure()
    {
        var fixture = new Fixture(options: new WebFetchToolOptions { MaximumHeaderCharacters = 5 });
        fixture.Resolver.Script(fixture.Origin, new NetworkResolved([Address()]));
        fixture.Transport.Script(fixture.Origin, Response("ok", "text/plain", 200));

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.FailureReason!.ShouldContain("headers exceeded");
    }

    [Fact]
    public async Task InvokeAsync_WhenContentIsUnsupportedMediaType_ReturnsUnsupportedContentFailure()
    {
        var fixture = new Fixture();
        fixture.Resolver.Script(fixture.Origin, new NetworkResolved([Address()]));
        fixture.Transport.Script(fixture.Origin, Response("binary", "image/png", 200));

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.ResultNormalizationFailed);
    }

    [Theory]
    [InlineData("bad-url")]
    [InlineData("ftp://example.test/")]
    public async Task InvokeAsync_WhenUrlSchemeUnsupportedOrMalformed_RejectsBeforeAuthorization(string url)
    {
        var fixture = new Fixture();

        var result = await fixture.Tool.InvokeAsync(
            Request($$"""{"url":"{{url}}"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        fixture.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenTimeoutMsIsInvalid_RejectsBeforeAuthorization()
    {
        var fixture = new Fixture();

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/","timeout_ms":0}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        fixture.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenTimeoutMsExceedsMaximum_RejectsBeforeAuthorization()
    {
        var fixture = new Fixture(options: new WebFetchToolOptions { DefaultTimeout = TimeSpan.FromSeconds(1), MaximumTimeout = TimeSpan.FromSeconds(5) });

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/","timeout_ms":10000}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        fixture.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenTimeoutMsSuppliedAndValid_UsesRequestedTimeout()
    {
        var fixture = new Fixture();
        fixture.ScriptSuccess(fixture.Origin, "ok", "text/plain");

        var result = await fixture.Tool.InvokeAsync(
            Request(/*lang=json,strict*/ """{"url":"https://example.test/","timeout_ms":5000}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
    }

    private sealed class ThrowingNetworkResponse(Exception exception): INetworkResponse
    {
        public NetworkResponseMetadata Metadata { get; } = new(200, new NetworkHeaderSet([new NetworkHeader("Content-Type", "text/plain")]), 0);

        public Stream Content { get; } = new ThrowingStream(exception);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ThrowingStream(Exception exception): Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw exception;
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw exception;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => throw exception;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
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
        TestSecurityEvidence.ToolContext(
            new AgentId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
            new SessionId(Guid.Parse("50000000-0000-0000-0000-000000000005")),
            new ToolCallId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
            new InRunOperationCorrelation(
                new OperationId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
                new RunId(Guid.Parse("80000000-0000-0000-0000-000000000008")),
                null),
            TestExecutionIdentity.Create(
                new TenantId("tenant"),
                new PrincipalId("principal"),
                ExecutionSubjectKind.Human)),
        JsonDocument.Parse(json).RootElement,
        DateTimeOffset.UnixEpoch);

    private sealed class Fixture
    {
        internal Fixture(TimeProvider? clock = null, WebFetchToolOptions? options = null)
        {
            Store = new StrictGrantStore();
            Authority = new RecordingSecurityAuthority(Store);
            Resolver = new ScriptedNetworkNameResolver(Store, new FixedTimeProvider());
            Transport = new ScriptedNetworkTransport(Store, new FixedTimeProvider());
            Tool = new WebFetchTool(
                Resolver,
                Transport,
                new FixedSecurityAuthoritySelector(Authority),
                new SequenceSecurityRequestIdGenerator(),
                new SequenceNetworkOperationIdGenerator(),
                clock ?? new FixedTimeProvider(),
                Options.Create(options ?? new WebFetchToolOptions()));
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
