// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.Tests;

public sealed class DefaultNetworkTransportTests
{
    [Fact]
    public void Constructor_WhenAnyDependencyIsNull_ThrowsArgumentNullException()
    {
        var timeProvider = TimeProvider.System;
        var options = Options.Create(new AgentNetworkOptions());

        _ = Should.Throw<ArgumentNullException>(() => new DefaultNetworkTransport(null!, options));
        _ = Should.Throw<ArgumentNullException>(() => new DefaultNetworkTransport(timeProvider, null!));
    }

    [Fact]
    public async Task SendAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        using var transport = TestFactory.Transport();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => transport.SendAsync(null!, TestContext.Current.CancellationToken).AsTask());

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task SendAsync_WhenServerReturnsOk_ReturnsResponseWithMatchingBodyAndStatus()
    {
        await using var server = LoopbackHttpServer.Start(static _ => new HttpListenerResponseSpec(
            200, [], "hello world"u8.ToArray(), "hello world"u8.Length));
        using var transport = TestFactory.Transport();

        var result = await transport.SendAsync(
            TestFactory.Request(TestFactory.LoopbackDestination(server.Port)), TestContext.Current.CancellationToken);

        var received = result.ShouldBeOfType<NetworkResponseReceived>();
        received.Response.Metadata.StatusCode.ShouldBe(200);
        using var reader = new StreamReader(received.Response.Content);
        var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        body.ShouldBe("hello world");
        await received.Response.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_WhenPostingContent_ServerReceivesExactBody()
    {
        string? receivedBody = null;
        await using var server = LoopbackHttpServer.Start(request =>
        {
            using var reader = new StreamReader(request.InputStream);
            receivedBody = reader.ReadToEnd();
            return new HttpListenerResponseSpec(200, [], ReadOnlyMemory<byte>.Empty, 0);
        });
        using var transport = TestFactory.Transport();

        var request = new NetworkRequest(
            new NetworkOperationId(Guid.NewGuid()),
            NetworkMethod.Post,
            TestFactory.LoopbackDestination(server.Port),
            NetworkHeaderSet.Empty,
            new NetworkRequestContent("text/plain", "payload"u8.ToArray()),
            TestFactory.Bounds(),
            [TestFactory.Address()]);

        var result = await transport.SendAsync(request, TestContext.Current.CancellationToken);

        var received = result.ShouldBeOfType<NetworkResponseReceived>();
        await received.Response.DisposeAsync();
        receivedBody.ShouldBe("payload");
    }

    [Fact]
    public async Task SendAsync_WhenPolicyRejectsDestination_ReturnsDenied()
    {
        var policy = new NetworkDestinationPolicy(["https"], null, allowPrivateAddresses: true);
        using var transport = TestFactory.Transport(policy);

        var result = await transport.SendAsync(
            TestFactory.Request(TestFactory.LoopbackDestination(1)), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<NetworkDenied>();
    }

    [Fact]
    public async Task SendAsync_WhenResponseDeclaresContentLengthAboveBound_ReturnsResponseLimitExceededWithoutReadingBody()
    {
        await using var server = LoopbackHttpServer.Start(static _ => new HttpListenerResponseSpec(
            200, [], new byte[1000], 1000));
        using var transport = TestFactory.Transport();

        var request = TestFactory.Request(TestFactory.LoopbackDestination(server.Port), bounds: TestFactory.Bounds(maximumResponseBytes: 10));
        var result = await transport.SendAsync(request, TestContext.Current.CancellationToken);

        var limitExceeded = result.ShouldBeOfType<NetworkResponseLimitExceeded>();
        limitExceeded.MaximumBytes.ShouldBe(10);
    }

    [Fact]
    public async Task SendAsync_WhenStreamedResponseExceedsBoundWithoutDeclaredLength_ThrowsWhileReading()
    {
        await using var server = LoopbackHttpServer.Start(static _ => new HttpListenerResponseSpec(
            200, [], new byte[1000], contentLength: null));
        using var transport = TestFactory.Transport();

        var request = TestFactory.Request(TestFactory.LoopbackDestination(server.Port), bounds: TestFactory.Bounds(maximumResponseBytes: 10));
        var result = await transport.SendAsync(request, TestContext.Current.CancellationToken);

        var received = result.ShouldBeOfType<NetworkResponseReceived>();
        var buffer = new byte[2000];
        _ = await Should.ThrowAsync<NetworkResponseTooLargeException>(async () =>
        {
            var total = 0;
            var read = await received.Response.Content.ReadAsync(buffer.AsMemory(total), TestContext.Current.CancellationToken);
            while (read > 0)
            {
                total += read;
                read = await received.Response.Content.ReadAsync(buffer.AsMemory(total), TestContext.Current.CancellationToken);
            }
        });

        await received.Response.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_WhenServerRedirects_ReturnsDestinationWithoutFollowing()
    {
        await using var target = LoopbackHttpServer.Start(static _ => new HttpListenerResponseSpec(200, [], "final"u8.ToArray(), 5));
        await using var redirector = LoopbackHttpServer.Start(_ => new HttpListenerResponseSpec(
            302, [new NetworkHeader("Location", $"http://127.0.0.1:{target.Port}/")], ReadOnlyMemory<byte>.Empty, 0));
        using var transport = TestFactory.Transport();

        var result = await transport.SendAsync(
            TestFactory.Request(TestFactory.LoopbackDestination(redirector.Port)), TestContext.Current.CancellationToken);

        var redirect = result.ShouldBeOfType<NetworkRedirectReceived>();
        redirect.Destination.Port.ShouldBe(target.Port);
        redirect.CrossOrigin.ShouldBeTrue();
    }

    [Fact]
    public async Task SendAsync_WhenSameOriginRedirect_ReportsNotCrossOrigin()
    {
        await using var server = LoopbackHttpServer.Start(request => new HttpListenerResponseSpec(
            302, [new NetworkHeader("Location", request.Url!.ToString())], ReadOnlyMemory<byte>.Empty, 0));
        using var transport = TestFactory.Transport();

        var request = TestFactory.Request(
            TestFactory.LoopbackDestination(server.Port), bounds: TestFactory.Bounds(maximumRedirects: 2));
        var result = await transport.SendAsync(request, TestContext.Current.CancellationToken);

        var redirect = result.ShouldBeOfType<NetworkRedirectReceived>();
        redirect.CrossOrigin.ShouldBeFalse();
    }

    [Fact]
    public async Task SendAsync_WhenCrossOriginRedirect_DoesNotForwardAuthorizationHeader()
    {
        var forwardedAuthorization = "not-observed";
        await using var target = LoopbackHttpServer.Start(request =>
        {
            forwardedAuthorization = request.Headers["Authorization"];
            return new HttpListenerResponseSpec(200, [], ReadOnlyMemory<byte>.Empty, 0);
        });
        await using var redirector = LoopbackHttpServer.Start(_ => new HttpListenerResponseSpec(
            302, [new NetworkHeader("Location", $"http://127.0.0.1:{target.Port}/")], ReadOnlyMemory<byte>.Empty, 0));
        using var transport = TestFactory.Transport();

        var request = new NetworkRequest(
            new NetworkOperationId(Guid.NewGuid()),
            NetworkMethod.Get,
            TestFactory.LoopbackDestination(redirector.Port),
            new NetworkHeaderSet([new NetworkHeader("Authorization", "Bearer secret")]),
            content: null,
            TestFactory.Bounds(),
            [TestFactory.Address()]);

        var result = await transport.SendAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<NetworkRedirectReceived>();
        forwardedAuthorization.ShouldBe("not-observed");
    }

    [Fact]
    public async Task SendAsync_WhenResponseTimeoutElapses_ReturnsTimeoutFailure()
    {
        await using var server = LoopbackHttpServer.Start(static _ =>
        {
            Thread.Sleep(500);
            return new HttpListenerResponseSpec(200, [], ReadOnlyMemory<byte>.Empty, 0);
        });
        using var transport = TestFactory.Transport();

        var request = TestFactory.Request(
            TestFactory.LoopbackDestination(server.Port),
            bounds: TestFactory.Bounds(responseTimeout: TimeSpan.FromMilliseconds(20)));
        var result = await transport.SendAsync(request, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<NetworkRequestFailed>();
        failed.Kind.ShouldBe(NetworkFailureKind.Timeout);
    }

    [Fact]
    public async Task SendAsync_WhenCallerCancels_ReturnsCancelled()
    {
        await using var server = LoopbackHttpServer.Start(static _ =>
        {
            Thread.Sleep(500);
            return new HttpListenerResponseSpec(200, [], ReadOnlyMemory<byte>.Empty, 0);
        });
        using var transport = TestFactory.Transport();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await transport.SendAsync(TestFactory.Request(TestFactory.LoopbackDestination(server.Port)), cts.Token);

        _ = result.ShouldBeOfType<NetworkCancelled>();
    }

    [Fact]
    public async Task SendAsync_WhenNoServerListening_ReturnsConnectionFailedWithSideEffectCertain()
    {
        using var transport = TestFactory.Transport();
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var unusedPort = ((IPEndPoint) probe.LocalEndpoint).Port;
        probe.Stop();

        var result = await transport.SendAsync(
            TestFactory.Request(TestFactory.LoopbackDestination(unusedPort)), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<NetworkRequestFailed>();
        failed.Kind.ShouldBe(NetworkFailureKind.ConnectionFailed);
        failed.SideEffectCertain.ShouldBeTrue();
    }
}
