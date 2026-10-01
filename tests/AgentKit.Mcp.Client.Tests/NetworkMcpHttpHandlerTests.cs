// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;

using System.Net;
using System.Net.Http;
using System.Text;

using AgentKit.Observability;

/// <summary>Verifies <see cref="NetworkMcpHttpHandler"/> routes every SDK HTTP exchange through the protected network boundary.</summary>
public sealed class NetworkMcpHttpHandlerTests
{
    private const string Token = "bearer-token-needle";
    private const string Body = /*lang=json,strict*/ """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"arguments":{"secret":"payload-needle"}}}""";

    private static StubHttpMessageHandler Ok(string body = "{}", string contentType = "application/json") =>
        new(_request =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, contentType),
            };
            _ = response.Headers.TryAddWithoutValidation("Mcp-Session-Id", "session-from-server");
            return response;
        });

    private static HttpRequestMessage Post(string uri = "https://mcp.example/rpc", string body = Body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        _ = request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + Token);
        _ = request.Headers.TryAddWithoutValidation("Mcp-Session-Id", "session-1");
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        return request;
    }

    [Fact]
    public async Task SendAsync_WhenPost_SendsFrozenBodyAndHeadersThroughTheNetworkTransportWithExactBounds()
    {
        using var harness = NetworkMcpHttpHandlerHarness.Create(Ok());
        using var request = Post();

        using var response = await harness.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sent = harness.Requests.ShouldHaveSingleItem();
        sent.Method.ShouldBe(NetworkMethod.Post);
        sent.Destination.Scheme.ShouldBe("https");
        sent.Destination.Host.Value.ShouldBe("mcp.example");
        sent.Destination.Port.ShouldBe(443);
        sent.Destination.Route.Value.ShouldBe("/rpc");
        var content = sent.Content.ShouldBeOfType<NetworkRequestContent>();
        content.ContentType.ShouldStartWith("application/json");
        Encoding.UTF8.GetString(content.Body.Span).ShouldBe(Body);
        sent.Headers.Headers.ShouldContain(new NetworkHeader("Authorization", "Bearer " + Token));
        sent.Headers.Headers.ShouldContain(new NetworkHeader("Mcp-Session-Id", "session-1"));
        sent.Headers.Headers.Count(static header => header.Name == "Accept").ShouldBe(2);
        sent.Classification.ShouldBe(NetworkDataClassification.Confidential);
        sent.Bounds.Resolution.ResolutionTimeout.ShouldBe(TimeSpan.FromSeconds(5));
        sent.Bounds.Request.ConnectTimeout.ShouldBe(TimeSpan.FromSeconds(5));
        sent.Bounds.Request.MaximumRequestBytes.ShouldBe(4096);
        sent.Bounds.Response.ResponseTimeout.ShouldBe(TimeSpan.FromSeconds(30));
        sent.Bounds.Response.MaximumResponseBytes.ShouldBe(8192);
        sent.Bounds.Response.MaximumRedirects.ShouldBe(0);
    }

    [Fact]
    public async Task SendAsync_WhenExchangeAuthorized_ObtainsResolutionAndSendGrantsBoundToTheCapturedOperation()
    {
        using var harness = NetworkMcpHttpHandlerHarness.Create(Ok());
        using var request = Post("https://mcp.example/rpc?trace=needle-query");
        var operation = ProviderEgressHarness.Operation;

        using var response = await harness.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);

        var requests = harness.Authority.Requests;
        requests.Count.ShouldBe(2);
        requests.ShouldAllBe(item => item.Kind == SecurityOperationKind.Network
            && item.Effect == SecurityEffect.Egress
            && item.ToolCallId == null
            && item.Authorization == operation.Authorization
            && item.Identity == operation.Identity);
        requests.Select(static item => item.Audience).ShouldBe([harness.Resolver.SecurityAudience, harness.Transport.SecurityAudience]);
        requests[0].Resources.ShouldHaveSingleItem().Identifier.ShouldBe("https://mcp.example:443");
        var send = requests[1];
        send.Resources.Length.ShouldBe(2);
        send.Resources[0].Identifier.ShouldStartWith("https://mcp.example:443/rpc?query=");
        send.Resources[0].Identifier.ShouldNotContain("needle-query");
        send.Resources[1].Identifier.ShouldBe("93.184.216.34:443");
        send.InputFingerprint.ShouldBe(NetworkSecurityBinding.RequestFingerprint(harness.Requests.Single()));
        foreach (var item in requests)
        {
            item.Resources.ShouldAllBe(static resource => !resource.Identifier.Contains(Token, StringComparison.Ordinal));
            item.Deadline.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
        }

        harness.Grants.Enforcements.Select(static item => item.Audience)
            .ShouldBe([harness.Resolver.SecurityAudience, harness.Transport.SecurityAudience]);
    }

    [Theory]
    [InlineData("GET", true)]
    [InlineData("POST", false)]
    [InlineData("DELETE", false)]
    public async Task SendAsync_WhenMethodVaries_BoundsResponseByStreamTimeoutOnlyForGet(string method, bool stream)
    {
        using var harness = NetworkMcpHttpHandlerHarness.Create(Ok());
        using var request = new HttpRequestMessage(new HttpMethod(method), "https://mcp.example/rpc");

        using var response = await harness.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);

        harness.Requests.ShouldHaveSingleItem().Bounds.Response.ResponseTimeout
            .ShouldBe(stream ? TimeSpan.FromMinutes(7) : TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task SendAsync_WhenResponseIsAStream_ReturnsBoundedBodyHeadersAndOwnsTheResponse()
    {
        StreamNetworkResponse? owned = null;
        using var harness = NetworkMcpHttpHandlerHarness.Create(grants => new CallbackNetworkTransport(
            grants,
            (_, _) =>
            {
                owned = new StreamNetworkResponse(
                    200,
                    new MemoryStream("data: hello\n\n"u8.ToArray()),
                    new NetworkHeader("Content-Type", "text/event-stream"),
                    new NetworkHeader("Mcp-Session-Id", "abc"));
                return ValueTask.FromResult<NetworkSendResult>(new NetworkResponseReceived(owned));
            }));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://mcp.example/rpc");

        var response = await harness.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);

        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");
        response.Headers.GetValues("Mcp-Session-Id").ShouldBe(["abc"]);
        owned.ShouldNotBeNull().IsDisposed.ShouldBeFalse();
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("data: hello\n\n");
        response.Dispose();
        owned.IsDisposed.ShouldBeTrue();
    }

    [Theory]
    [InlineData("test.network.resolver", 0, 0)]
    [InlineData("test.network.transport", 1, 0)]
    public async Task SendAsync_WhenAuthorityDeniesAGrant_ThrowsBeforeLaterEffects(string deniedAudience, int resolutions, int sends)
    {
        using var harness = NetworkMcpHttpHandlerHarness.Create(Ok());
        harness.Authority.Deny = item => item.Audience.Value == deniedAudience;
        using var request = Post();

        var exception = await Should.ThrowAsync<HttpRequestException>(
            () => harness.Client.SendAsync(request, TestContext.Current.CancellationToken));

        exception.Message.ShouldNotContain(Token);
        harness.Resolver.Resolved.Count.ShouldBe(resolutions);
        harness.Requests.Count.ShouldBe(sends);
        harness.Logger.Snapshot().ShouldHaveSingleItem().EventId.Id.ShouldBe(13030);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendAsync_WhenAuthorityIsUnavailable_FailsClosedWithoutResolutionOrSend(bool selectorThrows)
    {
        var selector = new UnavailableSelector(selectorThrows);
        using var harness = NetworkMcpHttpHandlerHarness.Create(Ok(), selector: selector);
        using var request = Post();

        var exception = await Should.ThrowAsync<HttpRequestException>(
            () => harness.Client.SendAsync(request, TestContext.Current.CancellationToken));

        exception.Message.ShouldNotContain(Token);
        harness.Authority.Requests.ShouldBeEmpty();
        harness.Resolver.Resolved.ShouldBeEmpty();
        harness.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenAuthorityThrows_FailsClosedWithoutLeakingTheFault()
    {
        using var harness = NetworkMcpHttpHandlerHarness.Create(Ok());
        harness.Authority.Fault = new InvalidOperationException("authority offline " + Token);
        using var request = Post();

        var exception = await Should.ThrowAsync<HttpRequestException>(
            () => harness.Client.SendAsync(request, TestContext.Current.CancellationToken));

        exception.Message.ShouldNotContain("offline");
        exception.Message.ShouldNotContain(Token);
        harness.Resolver.Resolved.ShouldBeEmpty();
        harness.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("POST", "https://other.example/rpc")]
    [InlineData("POST", "http://mcp.example/rpc")]
    [InlineData("POST", "https://mcp.example:8443/rpc")]
    [InlineData("POST", "https://user:pw@mcp.example/rpc")]
    [InlineData("POST", "https://mcp.example/rpc#fragment")]
    [InlineData("PUT", "https://mcp.example/rpc")]
    [InlineData("PATCH", "https://mcp.example/rpc")]
    public async Task SendAsync_WhenRequestLeavesTheConfiguredOriginOrMethods_RefusesBeforeAnyAuthorityOrNetworkCall(string method, string uri)
    {
        using var harness = NetworkMcpHttpHandlerHarness.Create(Ok());
        using var request = Post(uri);
        request.Method = new HttpMethod(method);

        var exception = await Should.ThrowAsync<HttpRequestException>(
            () => harness.Client.SendAsync(request, TestContext.Current.CancellationToken));

        exception.Message.ShouldNotContain("pw");
        exception.Message.ShouldNotContain(Token);
        harness.Authority.Requests.ShouldBeEmpty();
        harness.Resolver.Resolved.ShouldBeEmpty();
        harness.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenBodyExceedsTheMessageBound_RefusesBeforeAnyAuthorityCall()
    {
        using var harness = NetworkMcpHttpHandlerHarness.Create(Ok());
        using var request = Post(body: new string('x', 4097));

        _ = await Should.ThrowAsync<HttpRequestException>(
            () => harness.Client.SendAsync(request, TestContext.Current.CancellationToken));

        harness.Authority.Requests.ShouldBeEmpty();
        harness.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenServerRedirects_FailsWithoutFollowingAndWithoutForwardingCredentials()
    {
        var responder = new StubHttpMessageHandler(_request =>
        {
            var redirect = new HttpResponseMessage(HttpStatusCode.TemporaryRedirect);
            redirect.Headers.Location = new Uri("https://elsewhere.example/rpc");
            return redirect;
        });
        using var harness = NetworkMcpHttpHandlerHarness.Create(responder);
        using var request = Post();

        var exception = await Should.ThrowAsync<HttpRequestException>(
            () => harness.Client.SendAsync(request, TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("redirect");
        _ = harness.Requests.ShouldHaveSingleItem();
        responder.Seen.ShouldHaveSingleItem().Uri!.Host.ShouldBe("mcp.example");
    }

    [Fact]
    public async Task SendAsync_WhenDeclaredResponseExceedsTheBound_FailsWithConfigurationLimitError()
    {
        using var harness = NetworkMcpHttpHandlerHarness.Create(Ok(new string('x', 9000)));
        using var request = Post();

        var exception = await Should.ThrowAsync<HttpRequestException>(
            () => harness.Client.SendAsync(request, TestContext.Current.CancellationToken));

        exception.HttpRequestError.ShouldBe(HttpRequestError.ConfigurationLimitExceeded);
    }

    [Fact]
    public async Task SendAsync_WhenTransportDeniesSend_ThrowsRefusal()
    {
        using var harness = NetworkMcpHttpHandlerHarness.Create(grants => new CallbackNetworkTransport(
            grants,
            static (_, _) => ValueTask.FromResult<NetworkSendResult>(new NetworkDenied("Policy denied the destination."))));
        using var request = Post();

        var exception = await Should.ThrowAsync<HttpRequestException>(
            () => harness.Client.SendAsync(request, TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("Network egress was denied.");
    }

    [Fact]
    public async Task SendAsync_WhenCancelledBeforeStart_ThrowsWithoutAuthorityOrNetworkCall()
    {
        using var harness = NetworkMcpHttpHandlerHarness.Create(Ok());
        using var request = Post();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() => harness.Client.SendAsync(request, cancelled.Token));

        harness.Authority.Requests.ShouldBeEmpty();
        harness.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenCallerCancelsDuringSend_PropagatesCancellation()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var harness = NetworkMcpHttpHandlerHarness.Create(grants => new CallbackNetworkTransport(
            grants,
            async (_, token) =>
            {
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new NetworkCancelled(sideEffectCertain: false);
            }));
        using var request = Post();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var send = harness.Client.SendAsync(request, cancellation.Token);
        await entered.Task;
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() => send);
    }

    [Fact]
    public async Task SendAsync_WhenExchangeIsRefusedOrAnswered_LeavesCredentialsPayloadAndRouteOutOfEverySignal()
    {
        using var harness = NetworkMcpHttpHandlerHarness.Create(Ok());
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            static _ => true);
        using var metrics = new MetricCollector(AgentKitMetricNames.McpClientOperationCount);
        using var answered = Post("https://mcp.example/rpc?trace=needle-query");
        using var refused = Post("https://other.example/rpc");

        using var response = await harness.Client.SendAsync(answered, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
        _ = await Should.ThrowAsync<HttpRequestException>(() => harness.Client.SendAsync(refused, TestContext.Current.CancellationToken));

        harness.Logger.Snapshot().Select(static entry => entry.EventId.Id).ShouldBe([13032, 13030]);
        SignalAssertions.ShouldNotContainContent(
            activities.Snapshot(),
            harness.Logger.Snapshot(),
            metrics.Snapshot(),
            Token,
            "payload-needle",
            "needle-query",
            "session-1",
            "other.example");
    }

    [Fact]
    public void Constructor_WhenArgumentIsInvalid_ThrowsWithTheParameterName()
    {
        using var harness = NetworkMcpHttpHandlerHarness.Create(Ok());
        var operation = ProviderEgressHarness.Operation;
        var selector = new FixedSecurityAuthoritySelector(harness.Authority);
        var requestIds = new FixedIdentifier<SecurityRequestId>(default);
        var operationIds = new FixedIdentifier<NetworkOperationId>(default);
        var clock = TimeProvider.System;

        Should.Throw<ArgumentNullException>(() => new NetworkMcpHttpHandler(null!, harness.Transport, selector, requestIds, operationIds, operation, harness.McpEndpoint, harness.Options, clock, null))
            .ParamName.ShouldBe("resolver");
        Should.Throw<ArgumentNullException>(() => new NetworkMcpHttpHandler(harness.Resolver, null!, selector, requestIds, operationIds, operation, harness.McpEndpoint, harness.Options, clock, null))
            .ParamName.ShouldBe("transport");
        Should.Throw<ArgumentNullException>(() => new NetworkMcpHttpHandler(harness.Resolver, harness.Transport, null!, requestIds, operationIds, operation, harness.McpEndpoint, harness.Options, clock, null))
            .ParamName.ShouldBe("authorities");
        Should.Throw<ArgumentNullException>(() => new NetworkMcpHttpHandler(harness.Resolver, harness.Transport, selector, null!, operationIds, operation, harness.McpEndpoint, harness.Options, clock, null))
            .ParamName.ShouldBe("securityRequestIds");
        Should.Throw<ArgumentNullException>(() => new NetworkMcpHttpHandler(harness.Resolver, harness.Transport, selector, requestIds, null!, operation, harness.McpEndpoint, harness.Options, clock, null))
            .ParamName.ShouldBe("operationIds");
        Should.Throw<ArgumentNullException>(() => new NetworkMcpHttpHandler(harness.Resolver, harness.Transport, selector, requestIds, operationIds, null!, harness.McpEndpoint, harness.Options, clock, null))
            .ParamName.ShouldBe("operation");
        Should.Throw<ArgumentNullException>(() => new NetworkMcpHttpHandler(harness.Resolver, harness.Transport, selector, requestIds, operationIds, operation, null!, harness.Options, clock, null))
            .ParamName.ShouldBe("endpoint");
        Should.Throw<ArgumentNullException>(() => new NetworkMcpHttpHandler(harness.Resolver, harness.Transport, selector, requestIds, operationIds, operation, harness.McpEndpoint, null!, clock, null))
            .ParamName.ShouldBe("options");
        Should.Throw<ArgumentNullException>(() => new NetworkMcpHttpHandler(harness.Resolver, harness.Transport, selector, requestIds, operationIds, operation, harness.McpEndpoint, harness.Options, null!, null))
            .ParamName.ShouldBe("timeProvider");

        var stdio = new McpEndpoint(
            new McpEndpointKey("local"),
            new McpEndpointRevision(1),
            new McpStdioTransportProfile("tool", []),
            authentication: null,
            harness.Options.CreateEndpointBounds());
        Should.Throw<ArgumentException>(() => new NetworkMcpHttpHandler(harness.Resolver, harness.Transport, selector, requestIds, operationIds, operation, stdio, harness.Options, clock, null))
            .ParamName.ShouldBe("endpoint");
    }

    private sealed class FixedIdentifier<TId>(TId value): IIdentifierGenerator<TId>
        where TId : struct
    {
        public TId Create() => value;
    }

    private sealed class UnavailableSelector(bool throws): ISecurityAuthoritySelector
    {
        public ValueTask<SecurityAuthoritySelectionResult> SelectAsync(
            SecurityAuthorizationContext authorization,
            CancellationToken cancellationToken = default) =>
            throws
                ? throw new InvalidOperationException("selector offline " + Token)
                : ValueTask.FromResult<SecurityAuthoritySelectionResult>(
                    new SecurityAuthoritySelectionUnavailable(authorization, "No authority."));
    }
}
