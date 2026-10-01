// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Server.Tests;

/// <summary>Verifies <see cref="AgentKitPrimitiveHandler"/> argument handling and request observation.</summary>
public sealed class AgentKitPrimitiveHandlerTests
{
    [Fact]
    public void Constructor_WhenServicesAreNull_ThrowsArgumentNullExceptionNamingServices()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new AgentKitPrimitiveHandler(null!));

        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public async Task HandleAsync_WhenPeerIsNull_ThrowsArgumentNullExceptionNamingPeer()
    {
        var handler = new AgentKitPrimitiveHandler(new ServiceCollection().BuildServiceProvider());

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await handler.HandleAsync(null!, ListRequest(), TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("peer");
    }

    [Fact]
    public async Task HandleAsync_WhenRequestIsNull_ThrowsArgumentNullExceptionNamingRequest()
    {
        var handler = new AgentKitPrimitiveHandler(new ServiceCollection().BuildServiceProvider());

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await handler.HandleAsync(new McpPeerContext(new McpServerKey("s")), null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task HandleAsync_WhenTheRequestIsNotAToolCall_ReturnsUnsupportedAndObservesIt()
    {
        var serverKey = $"server-{Guid.NewGuid():N}";
        var handler = new AgentKitPrimitiveHandler(new ServiceCollection().BuildServiceProvider());
        using var activities = Collect(serverKey);
        using var metrics = new MetricCollector(AgentKitMetricNames.McpServerOperationCount);

        var response = await handler.HandleAsync(new McpPeerContext(new McpServerKey(serverKey)), ListRequest(), TestContext.Current.CancellationToken);

        _ = response.ShouldBeOfType<McpResponseUnsupportedCapability>();
        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.GetTagItem(AgentKitTagNames.McpOperation).ShouldBe("unsupported");
        span.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("unsupported");
        metrics.Snapshot().ShouldContain(static measurement => measurement.Tags[AgentKitTagNames.Outcome]!.Equals("unsupported"));
    }

    [Fact]
    public async Task HandleAsync_WhenToolDispatchCollaboratorsAreMissing_DeniesWithoutLeakingArguments()
    {
        var serverKey = $"server-{Guid.NewGuid():N}";
        var logger = new RecordingLogger<McpServerObservation>();
        var services = new ServiceCollection();
        _ = services.AddSingleton<ILoggerFactory>(new SingleLoggerFactory(logger));
        using var provider = services.BuildServiceProvider();
        var handler = new AgentKitPrimitiveHandler(provider);
        using var activities = Collect(serverKey);
        using var arguments = JsonDocument.Parse("""{"password":"hunter2-secret"}""");
        var request = new McpToolsCallRequest(
            new McpRequestId(Guid.NewGuid()),
            McpServerOperationFactory.CreateDefault(),
            new ToolCallId(Guid.NewGuid()),
            "vault.open",
            arguments);

        var response = await handler.HandleAsync(new McpPeerContext(new McpServerKey(serverKey)), request, TestContext.Current.CancellationToken);

        _ = response.ShouldBeOfType<McpResponseDenied>();
        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.GetTagItem(AgentKitTagNames.McpOperation).ShouldBe("tools.call");
        span.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("denied");
        logger.Snapshot().ShouldHaveSingleItem().EventId.Id.ShouldBe(13200);
        SignalAssertions.ShouldNotContainContent(activities.Snapshot(), logger.Snapshot(), [], "hunter2-secret");
    }

    private static McpToolsListRequest ListRequest() =>
        new(new McpRequestId(Guid.NewGuid()), McpServerOperationFactory.CreateDefault());

    private static ActivityCollector Collect(string serverKey) =>
        new(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.McpServerRequest
                && Equals(observation.GetTagItem(AgentKitTagNames.McpServerKey), serverKey));

    private sealed class SingleLoggerFactory(ILogger logger): ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider) => throw new NotSupportedException();

        public ILogger CreateLogger(string categoryName) => logger;

        public void Dispose()
        {
        }
    }
}
