// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;

using System.IO.Pipelines;
using System.Text.Json.Nodes;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

/// <summary>
/// Verifies <see cref="SdkMcpToolCaller"/> and <see cref="McpToolClientFactory{TTools}.ConnectAsync"/>
/// against a real official-SDK server connected over an in-memory duplex transport, since every other
/// test in this project exercises the reflected client machinery through a fake <see cref="IMcpToolCaller"/>.
/// </summary>
public sealed class SdkIntegrationTests
{
    [Fact]
    public async Task ConnectAsync_WhenServerRespondsSuccessfully_RoundTripsStructuredContentAndCatalog()
    {
        var logger = new RecordingLogger<McpToolClientFactory<EchoTools>>();
        await using var session = await IntegrationSession.CreateAsync(
            new SingleLoggerFactory(logger), TestContext.Current.CancellationToken);

        var result = await session.Client.CallAsync(
            tools => tools.RunAsync(new EchoRequest("hello"), default), TestContext.Current.CancellationToken);

        result.Message.ShouldBe("HELLO");
        session.Client.Catalog.Tools.ShouldHaveSingleItem().Name.ShouldBe(new McpToolName("echo.run"));
        session.Client.ProtocolVersion.ToString().ShouldNotBeNullOrWhiteSpace();
        logger.Snapshot().ShouldContain(static entry => entry.EventId.Id == 13000);
    }

    [Fact]
    public async Task CallAsync_WhenServerToolReportsAnError_MapsToTypedInvocationException()
    {
        await using var session = await IntegrationSession.CreateAsync(cancellationToken: TestContext.Current.CancellationToken);

        var exception = await Should.ThrowAsync<McpToolInvocationException>(async () =>
            await session.Client.CallAsync(tools => tools.RunAsync(new EchoRequest("fail"), default), TestContext.Current.CancellationToken));

        exception.ToolName.ShouldBe(new McpToolName("echo.run"));
        exception.Message.ShouldContain("reported a tool error");
    }

    [Fact]
    public async Task ConnectAsync_WhenServerLacksTheContractTool_RejectsWithContractMismatch()
    {
        var (clientTransport, server, provider) = await IntegrationSession.CreateTransportAsync(TestContext.Current.CancellationToken);
        await using var disposableServer = server;
        await using var disposableProvider = provider;
        var logger = new RecordingLogger<McpToolClientFactory<MismatchedTools>>();
        var factory = new McpToolClientFactory<MismatchedTools>(new McpToolContract<MismatchedTools>());

        _ = await Should.ThrowAsync<McpToolContractMismatchException>(async () =>
            await factory.ConnectAsync(clientTransport, loggerFactory: new SingleLoggerFactory(logger), cancellationToken: TestContext.Current.CancellationToken));

        logger.Snapshot().ShouldContain(static entry => entry.EventId.Id == 13002);
    }

    [Fact]
    public async Task ConnectAsync_WhenCancelledBeforeCompletion_PropagatesOperationCanceled()
    {
        var (clientTransport, server, provider) = await IntegrationSession.CreateTransportAsync(TestContext.Current.CancellationToken);
        await using var disposableServer = server;
        await using var disposableProvider = provider;
        var factory = new McpToolClientFactory<EchoTools>(new McpToolContract<EchoTools>());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await factory.ConnectAsync(clientTransport, cancellationToken: cancellation.Token));
    }

    private abstract class EchoTools
    {
        [McpTool("echo.run", "1")]
        public abstract Task<EchoResponse> RunAsync(EchoRequest request, CancellationToken cancellationToken = default);
    }

    private abstract class MismatchedTools
    {
        [McpTool("does-not-exist", "1")]
        public abstract Task<EchoResponse> RunAsync(EchoRequest request, CancellationToken cancellationToken = default);
    }

    private sealed record EchoRequest(string Message);
    private sealed record EchoResponse(string Message);

    private sealed class SingleLoggerFactory(ILogger logger): ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => logger;

        public void Dispose()
        {
        }
    }

    private sealed class IntegrationSession: IAsyncDisposable
    {
        private readonly McpServer _server;
        private readonly ServiceProvider _provider;

        private IntegrationSession(McpToolClient<EchoTools> client, McpServer server, ServiceProvider provider)
        {
            Client = client;
            _server = server;
            _provider = provider;
        }

        internal McpToolClient<EchoTools> Client { get; }

        internal static async Task<IntegrationSession> CreateAsync(
            ILoggerFactory? loggerFactory = null, CancellationToken cancellationToken = default)
        {
            var (transport, server, provider) = await CreateTransportAsync(cancellationToken);
            try
            {
                var factory = new McpToolClientFactory<EchoTools>(new McpToolContract<EchoTools>());
                var client = await factory.ConnectAsync(transport, loggerFactory: loggerFactory, cancellationToken: cancellationToken);
                return new IntegrationSession(client, server, provider);
            }
            catch
            {
                await server.DisposeAsync();
                await provider.DisposeAsync();
                throw;
            }
        }

        internal static Task<(StreamClientTransport Transport, McpServer Server, ServiceProvider Provider)> CreateTransportAsync(
            CancellationToken cancellationToken)
        {
            var provider = new ServiceCollection().BuildServiceProvider();
            var clientToServer = new Pipe();
            var serverToClient = new Pipe();
            var tool = McpServerTool.Create(
                (EchoRequest request) => request.Message == "fail"
                    ? throw new InvalidOperationException("simulated tool failure")
                    : new EchoResponse(request.Message.ToUpperInvariant()),
                new McpServerToolCreateOptions
                {
                    Name = "echo.run",
                    UseStructuredContent = true,
                    Meta = new JsonObject { [McpMetadataKeys.ToolContractVersion] = "1" },
                });
            var options = new McpServerOptions
            {
                ServerInfo = new Implementation { Name = "test-server", Version = "1.0" },
                ToolCollection = new McpServerPrimitiveCollection<McpServerTool>(StringComparer.Ordinal),
            };
            options.ToolCollection.Add(tool);
            var server = McpServer.Create(
                new StreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream()),
                options,
                serviceProvider: provider);
            _ = server.RunAsync(cancellationToken);
            var clientTransport = new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream());
            return Task.FromResult((clientTransport, server, provider));
        }

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync();
            await _server.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}
