// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Server.Tests;

public sealed class McpClientServerIntegrationTests
{
    [Fact]
    public async Task CallAsync_WhenConnectedThroughInMemoryJsonRpc_RoundTripsNestedObjects()
    {
        await using var session = await IntegrationSession<WeatherContract, WeatherTools>.CreateAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        var result = await session.Client.CallAsync(
            tools => tools.GetAsync(
                new WeatherRequest(new Location("Lisbon", "PT"), 3),
                default),
            TestContext.Current.CancellationToken);

        result.Location.ShouldBe(new Location("Lisbon", "PT"));
        result.Forecasts.ShouldBe(["Sunny", "Cloudy", "Sunny"]);
        session.Client.ProtocolVersion.ShouldBe(McpProtocolVersions.July2026);
        session.Client.Catalog.Tools.ShouldHaveSingleItem().Version.ShouldBe(new ToolVersion("2.1"));
    }

    [Theory]
    [InlineData("2024-11-05", McpProtocolEra.Legacy)]
    [InlineData("2025-03-26", McpProtocolEra.Legacy)]
    [InlineData("2025-06-18", McpProtocolEra.Legacy)]
    [InlineData("2025-11-25", McpProtocolEra.Legacy)]
    [InlineData("2026-07-28", McpProtocolEra.Modern)]
    public async Task ConnectAsync_WhenExactRevisionRequired_NegotiatesThatRevision(
        string value,
        McpProtocolEra expectedEra)
    {
        var version = McpProtocolVersion.Parse(value);
        await using var session = await IntegrationSession<WeatherContract, WeatherTools>.CreateAsync(
            version,
            cancellationToken: TestContext.Current.CancellationToken);

        session.Client.ProtocolVersion.ShouldBe(version);
        session.Client.ProtocolVersion.Era.ShouldBe(expectedEra);
    }

    [Fact]
    public async Task CallAsync_WhenValueTaskContractUsed_RoundTripsStructuredResult()
    {
        await using var session = await IntegrationSession<ValueTaskContract, ValueTaskTools>.CreateAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        var result = await session.Client.CallAsync(
            tools => tools.EchoAsync(new EchoRequest("hello"), default),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new EchoResponse("HELLO"));
    }

    [Fact]
    public async Task CallAsync_WhenCallsRunConcurrently_CorrelatesEveryStructuredResult()
    {
        await using var session = await IntegrationSession<ValueTaskContract, ValueTaskTools>.CreateAsync(
            cancellationToken: TestContext.Current.CancellationToken);
        var calls = Enumerable.Range(0, 32)
            .Select(index => session.Client.CallAsync(
                tools => tools.EchoAsync(new EchoRequest($"call-{index}"), default),
                TestContext.Current.CancellationToken).AsTask())
            .ToArray();

        var results = await Task.WhenAll(calls);

        results.Select(static result => result.Value)
            .ShouldBe(Enumerable.Range(0, 32).Select(static index => $"CALL-{index}"));
    }

    [Fact]
    public async Task CallAsync_WhenToolThrows_MapsRemoteErrorToTypedInvocationFailure()
    {
        await using var session = await IntegrationSession<ThrowingContract, ThrowingTools>.CreateAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        var exception = await Should.ThrowAsync<McpToolInvocationException>(
            async () => await session.Client.CallAsync(
                tools => tools.FailAsync(new EchoRequest("secret server detail"), default),
                TestContext.Current.CancellationToken));

        exception.ToolName.ShouldBe(new McpToolName("failure.throw"));
        exception.Message.ShouldContain("reported a tool error");
        exception.Message.ShouldNotContain("secret server detail");
    }

    [Fact]
    public async Task ConnectAsync_WhenLiveServerToolVersionDiffers_RejectsBeforeToolEffect()
    {
        VersionMismatchTools.InvocationCount = 0;

        var exception = await Should.ThrowAsync<McpToolContractMismatchException>(
            async () => await IntegrationSession<VersionTwoContract, VersionMismatchTools>.CreateAsync(
                cancellationToken: TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("2");
        exception.Message.ShouldContain("1");
        VersionMismatchTools.InvocationCount.ShouldBe(0);
    }

    [Fact]
    public async Task CallAsync_WhenToolHasConstructorDependency_ActivatesItFromRequestServices()
    {
        await using var session = await IntegrationSession<GreetingContract, GreetingTools>.CreateAsync(
            configureServices: static services => services.AddSingleton(new GreetingPrefix("Hello")),
            cancellationToken: TestContext.Current.CancellationToken);

        var result = await session.Client.CallAsync(
            tools => tools.GreetAsync(new EchoRequest("Alex"), default),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new EchoResponse("Hello, Alex"));
    }

    [Fact]
    public async Task CallAsync_WhenScopedDependencyUsed_CreatesAndDisposesOneScopePerInvocation()
    {
        var sequence = new ScopeSequence();
        await using var session = await IntegrationSession<ScopedContract, ScopedTools>.CreateAsync(
            configureServices: services =>
            {
                _ = services.AddSingleton(sequence);
                _ = services.AddScoped<InvocationScope>();
            },
            cancellationToken: TestContext.Current.CancellationToken);

        var first = await session.Client.CallAsync(
            tools => tools.GetScopeAsync(new EchoRequest("first"), default),
            TestContext.Current.CancellationToken);
        var second = await session.Client.CallAsync(
            tools => tools.GetScopeAsync(new EchoRequest("second"), default),
            TestContext.Current.CancellationToken);

        first.Value.ShouldBe("scope-1");
        second.Value.ShouldBe("scope-2");
        sequence.Disposed.ShouldBe(2);
    }

    [Fact]
    public async Task CallAsync_WhenClientCancels_CancelsLocalWaitWithoutLeakingCall()
    {
        var probe = new CancellationProbe();
        await using var session = await IntegrationSession<CancellableContract, CancellableTools>.CreateAsync(
            configureServices: services => services.AddSingleton(probe),
            cancellationToken: TestContext.Current.CancellationToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);

        var call = session.Client.CallAsync(
            tools => tools.WaitAsync(new EchoRequest("wait"), default),
            cancellation.Token).AsTask();
        await probe.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();

        try
        {
            _ = await Should.ThrowAsync<OperationCanceledException>(async () => await call);
        }
        finally
        {
            _ = probe.Release.TrySetResult();
        }
    }

    [Fact]
    public async Task CallAsync_WhenCustomNamingPolicyUsed_RoundTripsWithReflectedSchema()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        await using var session = await IntegrationSession<SerializerContract, SerializerTools>.CreateAsync(
            serializerOptions: options,
            cancellationToken: TestContext.Current.CancellationToken);

        var result = await session.Client.CallAsync(
            tools => tools.ResolveAsync(new SerializerRequest("1000-001"), default),
            TestContext.Current.CancellationToken);

        result.ShouldBe(new SerializerResponse("District 1000-001"));
        options.TypeInfoResolver.ShouldBeNull();
    }

    private abstract class WeatherContract
    {
        [McpTool("weather.get", "2.1", ReadOnly = true)]
        public abstract Task<WeatherResponse> GetAsync(
            WeatherRequest request,
            CancellationToken cancellationToken = default);
    }

    private sealed class WeatherTools: WeatherContract
    {
        public override Task<WeatherResponse> GetAsync(
            WeatherRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new WeatherResponse(
                request.Location,
                [.. Enumerable.Range(0, request.Days).Select(static day => day % 2 == 0 ? "Sunny" : "Cloudy")]));
        }
    }

    private sealed record Location(string City, string CountryCode);

    private sealed record WeatherRequest(Location Location, int Days);

    private sealed record WeatherResponse(Location Location, IReadOnlyList<string> Forecasts);

    private abstract class ValueTaskContract
    {
        [McpTool("echo.value-task", "1")]
        public abstract ValueTask<EchoResponse> EchoAsync(
            EchoRequest request,
            CancellationToken cancellationToken = default);
    }

    private sealed class ValueTaskTools: ValueTaskContract
    {
        public override ValueTask<EchoResponse> EchoAsync(
            EchoRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new EchoResponse(request.Value.ToUpperInvariant()));
        }
    }

    private abstract class ThrowingContract
    {
        [McpTool("failure.throw", "1")]
        public abstract Task<EchoResponse> FailAsync(
            EchoRequest request,
            CancellationToken cancellationToken = default);
    }

    private sealed class ThrowingTools: ThrowingContract
    {
        public override Task<EchoResponse> FailAsync(
            EchoRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(request.Value);
    }

    private abstract class VersionTwoContract
    {
        [McpTool("versioned", "2")]
        public abstract Task<EchoResponse> ExecuteAsync(EchoRequest request);
    }

#pragma warning disable CA1822 // Instance method is required to exercise the server reflection boundary.
    private sealed class VersionMismatchTools
    {
        public static int InvocationCount;

        [McpTool("versioned", "1")]
        public Task<EchoResponse> ExecuteAsync(EchoRequest request)
        {
            _ = Interlocked.Increment(ref InvocationCount);
            return Task.FromResult(new EchoResponse(request.Value));
        }
    }
#pragma warning restore CA1822

    private abstract class GreetingContract
    {
        [McpTool("greeting", "1")]
        public abstract Task<EchoResponse> GreetAsync(
            EchoRequest request,
            CancellationToken cancellationToken = default);
    }

    private sealed class GreetingTools(GreetingPrefix prefix): GreetingContract
    {
        public override Task<EchoResponse> GreetAsync(
            EchoRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new EchoResponse($"{prefix.Value}, {request.Value}"));
        }
    }

    private abstract class ScopedContract
    {
        [McpTool("scope.get", "1")]
        public abstract Task<EchoResponse> GetScopeAsync(
            EchoRequest request,
            CancellationToken cancellationToken = default);
    }

    private sealed class ScopedTools(InvocationScope scope): ScopedContract
    {
        public override Task<EchoResponse> GetScopeAsync(
            EchoRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new EchoResponse(scope.Id));
        }
    }

    private abstract class CancellableContract
    {
        [McpTool("cancel.wait", "1")]
        public abstract Task<EchoResponse> WaitAsync(
            EchoRequest request,
            CancellationToken cancellationToken = default);
    }

    private sealed class CancellableTools(CancellationProbe probe): CancellableContract
    {
        public override async Task<EchoResponse> WaitAsync(
            EchoRequest request,
            CancellationToken cancellationToken = default)
        {
            _ = probe.Entered.TrySetResult();
            await probe.Release.Task.WaitAsync(cancellationToken);
            return new EchoResponse(request.Value);
        }
    }

    private abstract class SerializerContract
    {
        [McpTool("serializer.resolve", "1")]
        public abstract Task<SerializerResponse> ResolveAsync(
            SerializerRequest request,
            CancellationToken cancellationToken = default);
    }

    private sealed class SerializerTools: SerializerContract
    {
        public override Task<SerializerResponse> ResolveAsync(
            SerializerRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new SerializerResponse($"District {request.PostalCode}"));
        }
    }

    private sealed record EchoRequest(string Value);

    private sealed record EchoResponse(string Value);

    private sealed record GreetingPrefix(string Value);

    private sealed record SerializerRequest(string PostalCode);

    private sealed record SerializerResponse(string DistrictName);

    private sealed class ScopeSequence
    {
        private int _created;
        private int _disposed;

        public int Disposed => Volatile.Read(ref _disposed);

        public string Next() => $"scope-{Interlocked.Increment(ref _created)}";

        public void MarkDisposed() => _ = Interlocked.Increment(ref _disposed);
    }

    private sealed class InvocationScope(ScopeSequence sequence): IDisposable
    {
        public string Id { get; } = sequence.Next();

        public void Dispose() => sequence.MarkDisposed();
    }

    private sealed class CancellationProbe
    {
        public TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class IntegrationSession<TContract, TImplementation>: IAsyncDisposable
        where TContract : class
        where TImplementation : class
    {
        private readonly ServiceProvider _provider;
        private readonly McpServer _server;

        private IntegrationSession(
            ServiceProvider provider,
            McpServer server,
            McpToolClient<TContract> client)
        {
            _provider = provider;
            _server = server;
            Client = client;
        }

        public McpToolClient<TContract> Client { get; }

        public static async Task<IntegrationSession<TContract, TImplementation>> CreateAsync(
            McpProtocolVersion? protocolVersion = null,
            JsonSerializerOptions? serializerOptions = null,
            Action<IServiceCollection>? configureServices = null,
            CancellationToken cancellationToken = default)
        {
            var services = new ServiceCollection();
            configureServices?.Invoke(services);
            var serverPolicy = protocolVersion is { } version
                ? McpServerVersionPolicy.Require(version)
                : McpServerVersionPolicy.Compatible;
            _ = services.AddAgentKitMcpServer(serverPolicy)
                .WithAgentKitTools<TImplementation>(serializerOptions);
            var provider = services.BuildServiceProvider();
            var clientToServer = new Pipe();
            var serverToClient = new Pipe();
            var server = McpServer.Create(
                new StreamServerTransport(
                    clientToServer.Reader.AsStream(),
                    serverToClient.Writer.AsStream()),
                provider.GetRequiredService<IOptions<McpServerOptions>>().Value,
                serviceProvider: provider);
            _ = server.RunAsync(cancellationToken);

            try
            {
                var clientPolicy = protocolVersion is { } required
                    ? McpClientVersionPolicy.RequireAtLeast(required)
                    : McpClientVersionPolicy.Automatic;
                var factory = new McpToolClientFactory<TContract>(new McpToolContract<TContract>());
                var client = await factory.ConnectAsync(
                    new StreamClientTransport(
                        clientToServer.Writer.AsStream(),
                        serverToClient.Reader.AsStream()),
                    clientPolicy,
                    serializerOptions,
                    cancellationToken: cancellationToken);
                return new IntegrationSession<TContract, TImplementation>(provider, server, client);
            }
            catch
            {
                await server.DisposeAsync();
                await provider.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync();
            await _server.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}
