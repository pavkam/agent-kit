// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Server.Tests;

/// <summary>Verifies the SDK call-tool filter registered by <c>AddAgentKitMcpServer</c>.</summary>
public sealed class McpServerToolCallFilterTests
{
    [Fact]
    public void Create_WhenNextIsNull_ThrowsArgumentNullExceptionNamingNext()
    {
        var exception = Should.Throw<ArgumentNullException>(() => McpServerToolCallFilter.Create(null!));

        exception.ParamName.ShouldBe("next");
    }

    [Fact]
    public async Task CallAsync_WhenTheSdkDispatchesAToolCall_ObservesItWithoutTheArguments()
    {
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            static observation => observation.OperationName == AgentKitActivityNames.McpServerToolCall
                && Equals(observation.GetTagItem(AgentKitTagNames.ToolName), "observed.echo"));
        await using var session = await Session.CreateAsync(TestContext.Current.CancellationToken);

        var result = await session.Client.CallAsync(
            tools => tools.EchoAsync(new Echo("classified-payload-4711"), default), TestContext.Current.CancellationToken);

        result.Value.ShouldBe("classified-payload-4711");
        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Ok);
        span.GetTagItem(AgentKitTagNames.McpOperation).ShouldBe("tools.call");
        span.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("succeeded");
        SignalAssertions.ShouldNotContainContent(activities.Snapshot(), [], [], "classified-payload-4711");
    }

    private abstract class EchoContract
    {
        [McpTool("observed.echo", "1")]
        public abstract Task<Echo> EchoAsync(Echo request, CancellationToken cancellationToken = default);
    }

    private sealed class EchoTools: EchoContract
    {
        public override Task<Echo> EchoAsync(Echo request, CancellationToken cancellationToken = default) => Task.FromResult(request);
    }

    private sealed record Echo(string Value);

    private sealed class Session: IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly McpServer _server;

        private Session(ServiceProvider provider, McpServer server, McpToolClient<EchoContract> client)
        {
            _provider = provider;
            _server = server;
            Client = client;
        }

        public McpToolClient<EchoContract> Client { get; }

        public static async Task<Session> CreateAsync(CancellationToken cancellationToken)
        {
            var services = new ServiceCollection();
            _ = services.AddAgentKitMcpServer().WithAgentKitTools<EchoTools>();
            var provider = services.BuildServiceProvider();
            var clientToServer = new Pipe();
            var serverToClient = new Pipe();
            var server = McpServer.Create(
                new StreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream()),
                provider.GetRequiredService<IOptions<McpServerOptions>>().Value,
                null,
                provider);
            _ = server.RunAsync(cancellationToken);
            var factory = new McpToolClientFactory<EchoContract>(new McpToolContract<EchoContract>());
            var client = await factory.ConnectAsync(
                new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()),
                McpClientVersionPolicy.Automatic,
                cancellationToken: cancellationToken);
            return new Session(provider, server, client);
        }

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync();
            await _server.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}
