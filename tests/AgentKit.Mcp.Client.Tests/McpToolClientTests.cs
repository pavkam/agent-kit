// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;



/// <summary>Verifies McpToolClient behavior and contracts.</summary>
public sealed class McpToolClientTests
{
    [Fact]
    public async Task CallAsync_WhenObserved_EmitsContentFreeCorrelatedActivities()
    {
        const string requestSecret = "Lisbon-request-secret";
        const string responseSecret = "sunny-response-secret";
        var stopped = new System.Collections.Concurrent.ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = stopped.Enqueue,
        };
        ActivitySource.AddActivityListener(listener);
        var caller = new FakeToolCaller(McpProtocolVersions.July2026, [WeatherRemoteTool()], JsonSerializer.SerializeToElement(new WeatherResponse(responseSecret)));
        await using var client = await McpToolClientActivator.CreateAsync<WeatherTools>(caller, cancellationToken: TestContext.Current.CancellationToken);
        using var parent = new Activity("test-parent").Start();
        _ = await client.CallAsync(tools => tools.GetAsync(new WeatherRequest(requestSecret), default), TestContext.Current.CancellationToken);
        parent.Stop();
        var activity = stopped.Single(item => item.OperationName == AgentKitActivityNames.McpToolCall && item.ParentSpanId == parent.SpanId);
        activity.ParentSpanId.ShouldBe(parent.SpanId);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.ToolName).ShouldBe("weather.get");
        activity.GetTagItem(AgentKitTagNames.McpCatalogVersion).ShouldBe(1L);
        var diagnosticText = string.Join('|', stopped.SelectMany(static item => item.TagObjects).Select(static tag => $"{tag.Key}={tag.Value}"));
        diagnosticText.ShouldNotContain(requestSecret);
        diagnosticText.ShouldNotContain(responseSecret);
    }

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
    [Fact]
    public async Task CallAsync_WhenContractMatches_SendsReflectedNameAndObject()
    {
        var caller = new FakeToolCaller(McpProtocolVersions.July2026, [new McpRemoteTool(new McpToolName("weather.get"), new ToolVersion("2.1"))], JsonSerializer.SerializeToElement(new WeatherResponse("sunny")));
        await using var client = await McpToolClientActivator.CreateAsync<WeatherTools>(caller, cancellationToken: TestContext.Current.CancellationToken);
        var request = new WeatherRequest("Lisbon");
        var result = await client.CallAsync(tools => tools.GetAsync(request, default), TestContext.Current.CancellationToken);
        result.ShouldBe(new WeatherResponse("sunny"));
        caller.LastMethod.ShouldNotBeNull().Name.ShouldBe(new McpToolName("weather.get"));
        caller.LastRequest.ShouldBeSameAs(request);
        client.Catalog.Version.ShouldBe(new McpCatalogVersion(1));
        client.ProtocolVersion.ShouldBe(McpProtocolVersions.July2026);
    }

    [Fact]
    public async Task CreateAsync_WhenRemoteVersionDiffers_RejectsBeforeInvocation()
    {
        var caller = new FakeToolCaller(McpProtocolVersions.July2026, [new McpRemoteTool(new McpToolName("weather.get"), new ToolVersion("1.0"))], JsonSerializer.SerializeToElement(new WeatherResponse("unused")));
        var exception = await Should.ThrowAsync<McpToolContractMismatchException>(async () => await McpToolClientActivator.CreateAsync<WeatherTools>(caller, cancellationToken: TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("2.1");
        exception.Message.ShouldContain("1.0");
        caller.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateAsync_WhenRequiredToolIsMissing_RejectsAndDisposesCaller()
    {
        var caller = CreateCaller([]);
        var exception = await Should.ThrowAsync<McpToolContractMismatchException>(async () => await McpToolClientActivator.CreateAsync<WeatherTools>(caller, cancellationToken: TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("weather.get");
        exception.Message.ShouldContain("2.1");
        caller.DisposeCount.ShouldBe(1);
        caller.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task CreateAsync_WhenRequiredToolVersionIsMissing_RejectsBeforeInvocation()
    {
        var caller = CreateCaller([new McpRemoteTool(new McpToolName("weather.get"), null)]);
        var exception = await Should.ThrowAsync<McpToolContractMismatchException>(async () => await McpToolClientActivator.CreateAsync<WeatherTools>(caller, cancellationToken: TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("<missing>");
        caller.DisposeCount.ShouldBe(1);
        caller.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task CreateAsync_WhenRemoteNamesAreDuplicated_RejectsAmbiguousCatalog()
    {
        var remote = new McpRemoteTool(new McpToolName("weather.get"), new ToolVersion("2.1"));
        var caller = CreateCaller([remote, remote]);
        var exception = await Should.ThrowAsync<McpToolContractMismatchException>(async () => await McpToolClientActivator.CreateAsync<WeatherTools>(caller, cancellationToken: TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("duplicate tool name");
        caller.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task CreateAsync_WhenRemoteCatalogHasAdditionalTools_PublishesOnlyContractMatches()
    {
        var caller = CreateCaller([WeatherRemoteTool(), new McpRemoteTool(new McpToolName("unrelated"), new ToolVersion("9"))]);
        await using var client = await McpToolClientActivator.CreateAsync<WeatherTools>(caller, cancellationToken: TestContext.Current.CancellationToken);
        client.Catalog.Tools.ShouldHaveSingleItem().Name.ShouldBe(new McpToolName("weather.get"));
    }

    [Fact]
    public async Task RefreshAsync_WhenCatalogChanges_PublishesNewImmutableVersion()
    {
        var caller = new FakeToolCaller(McpProtocolVersions.July2026, [new McpRemoteTool(new McpToolName("weather.get"), new ToolVersion("2.1"))], JsonSerializer.SerializeToElement(new WeatherResponse("sunny")));
        await using var client = await McpToolClientActivator.CreateAsync<WeatherTools>(caller, cancellationToken: TestContext.Current.CancellationToken);
        var original = client.Catalog;
        _ = await client.RefreshAsync(TestContext.Current.CancellationToken);
        original.Version.ShouldBe(new McpCatalogVersion(1));
        client.Catalog.Version.ShouldBe(new McpCatalogVersion(2));
        client.Catalog.ShouldNotBeSameAs(original);
    }

    [Fact]
    public async Task RefreshAsync_WhenLaterCatalogIsInvalid_PreservesPublishedSnapshotAndGeneration()
    {
        var caller = CreateCaller([WeatherRemoteTool()]);
        await using var client = await McpToolClientActivator.CreateAsync<WeatherTools>(caller, cancellationToken: TestContext.Current.CancellationToken);
        var original = client.Catalog;
        caller.Tools = [];
        _ = await Should.ThrowAsync<McpToolContractMismatchException>(async () => await client.RefreshAsync(TestContext.Current.CancellationToken));
        client.Catalog.ShouldBeSameAs(original);
        client.Catalog.Version.ShouldBe(new McpCatalogVersion(1));
        caller.Tools = [WeatherRemoteTool()];
        var recovered = await client.RefreshAsync(TestContext.Current.CancellationToken);
        recovered.Version.ShouldBe(new McpCatalogVersion(2));
    }

    [Fact]
    public async Task RefreshAsync_WhenTwoRefreshesRace_SerializesCatalogPublication()
    {
        var caller = CreateCaller([WeatherRemoteTool()]);
        await using var client = await McpToolClientActivator.CreateAsync<WeatherTools>(caller, cancellationToken: TestContext.Current.CancellationToken);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var maximumActive = 0;
        caller.ListToolsHandler = async cancellationToken =>
        {
            var current = Interlocked.Increment(ref active);
            maximumActive = Math.Max(maximumActive, current);
            if (caller.ListCount == 2)
            {
                firstEntered.SetResult();
                await releaseFirst.Task.WaitAsync(cancellationToken);
            }

            _ = Interlocked.Decrement(ref active);
            return caller.Tools;
        };
        var first = client.RefreshAsync(TestContext.Current.CancellationToken).AsTask();
        await firstEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
        var second = client.RefreshAsync(TestContext.Current.CancellationToken).AsTask();
        releaseFirst.SetResult();
        var results = await Task.WhenAll(first, second);
        maximumActive.ShouldBe(1);
        results.Select(static snapshot => snapshot.Version.Value).ShouldBe([2, 3]);
        client.Catalog.Version.ShouldBe(new McpCatalogVersion(3));
    }

    [Fact]
    public async Task RefreshAsync_WhenCancellationIsRequested_PropagatesWithoutPublishing()
    {
        var caller = CreateCaller([WeatherRemoteTool()]);
        await using var client = await McpToolClientActivator.CreateAsync<WeatherTools>(caller, cancellationToken: TestContext.Current.CancellationToken);
        var original = client.Catalog;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await client.RefreshAsync(cancellation.Token));
        client.Catalog.ShouldBeSameAs(original);
    }

    [Fact]
    public async Task CallAsync_WhenValueTaskMethodSelected_InvokesAndDeserializesResponse()
    {
        var caller = new FakeToolCaller(McpProtocolVersions.July2026, [new McpRemoteTool(new McpToolName("weather.value-task"), new ToolVersion("1"))], JsonSerializer.SerializeToElement(new WeatherResponse("cloudy")));
        await using var client = await McpToolClientActivator.CreateAsync<ValueTaskTools>(caller, cancellationToken: TestContext.Current.CancellationToken);
        var result = await client.CallAsync(tools => tools.GetAsync(new WeatherRequest("Porto"), default), TestContext.Current.CancellationToken);
        result.ShouldBe(new WeatherResponse("cloudy"));
        caller.LastMethod.ShouldNotBeNull().Name.ShouldBe(new McpToolName("weather.value-task"));
    }

    [Fact]
    public async Task CallAsync_WhenRequestComesFromClosure_PreservesObjectIdentity()
    {
        var caller = CreateCaller([WeatherRemoteTool()]);
        await using var client = await McpToolClientActivator.CreateAsync<WeatherTools>(caller, cancellationToken: TestContext.Current.CancellationToken);
        var holder = new RequestHolder(new WeatherRequest("Coimbra"));
        _ = await client.CallAsync(tools => tools.GetAsync(holder.Request, default), TestContext.Current.CancellationToken);
        caller.LastRequest.ShouldBeSameAs(holder.Request);
    }

    [Fact]
    public async Task CallAsync_WhenRequestEvaluatesToNull_RejectsBeforeRemoteCall()
    {
        var caller = CreateCaller([WeatherRemoteTool()]);
        await using var client = await McpToolClientActivator.CreateAsync<WeatherTools>(caller, cancellationToken: TestContext.Current.CancellationToken);
        WeatherRequest request = null!;
        var exception = await Should.ThrowAsync<ArgumentException>(async () => await client.CallAsync(tools => tools.GetAsync(request, default), TestContext.Current.CancellationToken));
        exception.ParamName.ShouldBe("expression");
        caller.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task CallAsync_WhenExpressionIsNotDirectMethodCall_RejectsBeforeRemoteCall()
    {
        var caller = new FakeToolCaller(McpProtocolVersions.July2026, [new McpRemoteTool(new McpToolName("property.execute"), new ToolVersion("1"))], JsonSerializer.SerializeToElement(new WeatherResponse("unused")));
        await using var client = await McpToolClientActivator.CreateAsync<PropertyTools>(caller, cancellationToken: TestContext.Current.CancellationToken);
        var exception = await Should.ThrowAsync<ArgumentException>(async () => await client.CallAsync(tools => tools.Pending, TestContext.Current.CancellationToken));
        exception.ParamName.ShouldBe("expression");
        caller.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task CallAsync_WhenExpressionCallsForeignMethod_RejectsBeforeRemoteCall()
    {
        var caller = CreateCaller([WeatherRemoteTool()]);
        await using var client = await McpToolClientActivator.CreateAsync<WeatherTools>(caller, cancellationToken: TestContext.Current.CancellationToken);
        var exception = await Should.ThrowAsync<ArgumentException>(async () => await client.CallAsync(tools => Forward(tools, new WeatherRequest("Faro")), TestContext.Current.CancellationToken));
        exception.ParamName.ShouldBe("method");
        caller.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task CallAsync_WhenStructuredContentIsJsonNull_RejectsNonNullResponse()
    {
        var caller = CreateCaller([WeatherRemoteTool()]);
        caller.Response = JsonSerializer.SerializeToElement<WeatherResponse?>(null);
        await using var client = await McpToolClientActivator.CreateAsync<WeatherTools>(caller, cancellationToken: TestContext.Current.CancellationToken);
        var exception = await Should.ThrowAsync<JsonException>(async () => await client.CallAsync(tools => tools.GetAsync(new WeatherRequest("Braga"), default), TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("JSON null");
        caller.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task LoggerFactory_WhenObservingTheCompleteLifecycle_EmitsEveryStructuredEvent()
    {
        var logger = new RecordingLogger<McpToolClientTests>();
        var loggerFactory = new SingleLoggerFactory(logger);
        var caller = CreateCaller([WeatherRemoteTool()]);
        await using var client = await McpToolClientActivator.CreateAsync<WeatherTools>(caller, loggerFactory: loggerFactory, cancellationToken: TestContext.Current.CancellationToken);

        _ = await client.CallAsync(tools => tools.GetAsync(new WeatherRequest("Porto"), default), TestContext.Current.CancellationToken);

        using var cancellation = new CancellationTokenSource();
        caller.CallHandler = (_, _, _, _) =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        };
        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await client.CallAsync(tools => tools.GetAsync(new WeatherRequest("Porto"), default), cancellation.Token));

        caller.CallHandler = static (_, _, _, _) => throw new InvalidOperationException("boom");
        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await client.CallAsync(tools => tools.GetAsync(new WeatherRequest("Porto"), default), TestContext.Current.CancellationToken));

        using var refreshCancellation = new CancellationTokenSource();
        caller.ListToolsHandler = async ct =>
        {
            refreshCancellation.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return [];
        };
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await client.RefreshAsync(refreshCancellation.Token));

        caller.ListToolsHandler = static _ => ValueTask.FromResult<IReadOnlyList<McpRemoteTool>>([]);
        _ = await Should.ThrowAsync<McpToolContractMismatchException>(async () => await client.RefreshAsync(TestContext.Current.CancellationToken));

        var events = logger.Snapshot().Select(static entry => entry.EventId.Id).ToArray();
        events.ShouldContain(13010); // CatalogPublished (initial activation)
        events.ShouldContain(13020); // ToolCallCompleted
        events.ShouldContain(13021); // ToolCallCancelled
        events.ShouldContain(13022); // ToolCallFailed
        events.ShouldContain(13011); // CatalogRefreshCancelled
        events.ShouldContain(13012); // CatalogRefreshFailed
    }

    [Fact]
    public async Task CallAsync_WhenCallerCancels_PropagatesCancellation()
    {
        var caller = CreateCaller([WeatherRemoteTool()]);
        await using var client = await McpToolClientActivator.CreateAsync<WeatherTools>(caller, cancellationToken: TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        caller.CallHandler = (_, _, _, _) =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        };

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await client.CallAsync(tools => tools.GetAsync(new WeatherRequest("Faro"), default), cancellation.Token));

        caller.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task CallAsync_WhenCallerFails_PropagatesFailureWithoutRetry()
    {
        var caller = CreateCaller([WeatherRemoteTool()]);
        caller.CallHandler = static (_, _, _, _) => throw new InvalidOperationException("transport failed");
        await using var client = await McpToolClientActivator.CreateAsync<WeatherTools>(caller, cancellationToken: TestContext.Current.CancellationToken);
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await client.CallAsync(tools => tools.GetAsync(new WeatherRequest("Evora"), default), TestContext.Current.CancellationToken));
        exception.Message.ShouldBe("transport failed");
        caller.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task CallAsync_WhenCustomSerializerProvided_UsesPrivateEquivalentOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
        var caller = CreateCaller([WeatherRemoteTool()]);
        await using var client = await McpToolClientActivator.CreateAsync<WeatherTools>(caller, serializerOptions: options, cancellationToken: TestContext.Current.CancellationToken);
        _ = await client.CallAsync(tools => tools.GetAsync(new WeatherRequest("Aveiro"), default), TestContext.Current.CancellationToken);
        caller.LastSerializerOptions.ShouldNotBeSameAs(options);
        caller.LastSerializerOptions.ShouldNotBeNull().PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.SnakeCaseLower);
        _ = caller.LastSerializerOptions.TypeInfoResolver.ShouldNotBeNull();
        options.TypeInfoResolver.ShouldBeNull();
    }

    [Fact]
    public async Task CallAsync_WhenClientIsDisposed_RejectsBeforeRemoteCall()
    {
        var caller = CreateCaller([WeatherRemoteTool()]);
        var client = await McpToolClientActivator.CreateAsync<WeatherTools>(caller, cancellationToken: TestContext.Current.CancellationToken);
        await client.DisposeAsync();
        _ = await Should.ThrowAsync<ObjectDisposedException>(async () => await client.CallAsync(tools => tools.GetAsync(new WeatherRequest("Leiria"), default), TestContext.Current.CancellationToken));
        caller.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task RefreshAsync_WhenClientIsDisposed_RejectsBeforeListing()
    {
        var caller = CreateCaller([WeatherRemoteTool()]);
        var client = await McpToolClientActivator.CreateAsync<WeatherTools>(caller, cancellationToken: TestContext.Current.CancellationToken);
        await client.DisposeAsync();
        var listCount = caller.ListCount;
        _ = await Should.ThrowAsync<ObjectDisposedException>(async () => await client.RefreshAsync(TestContext.Current.CancellationToken));
        caller.ListCount.ShouldBe(listCount);
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledTwice_DisposesOwnedCallerOnce()
    {
        var caller = CreateCaller([WeatherRemoteTool()]);
        var client = await McpToolClientActivator.CreateAsync<WeatherTools>(caller, cancellationToken: TestContext.Current.CancellationToken);
        await client.DisposeAsync();
        await client.DisposeAsync();
        caller.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public void RequireAtLeast_WhenVersionIsUninitialized_ThrowsForVersion()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => McpClientVersionPolicy.RequireAtLeast(default));
        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void Automatic_WhenRead_HasNoMinimumVersion() => McpClientVersionPolicy.Automatic.MinimumVersion.ShouldBeNull();
    [Theory]
    [InlineData("2025-11-25")]
    [InlineData("2026-07-28")]
    public void RequireAtLeast_WhenVersionIsInitialized_CapturesMinimum(string value)
    {
        var version = McpProtocolVersion.Parse(value);
        var policy = McpClientVersionPolicy.RequireAtLeast(version);
        policy.MinimumVersion.ShouldBe(version);
    }

    [Fact]
    public void With_WhenNoMemberIsChanged_ProducesAnEqualDistinctCopy()
    {
        var policy = McpClientVersionPolicy.RequireAtLeast(McpProtocolVersions.July2026);

        var copy = policy with { };

        copy.ShouldNotBeSameAs(policy);
        copy.ShouldBe(policy);
        copy.MinimumVersion.ShouldBe(policy.MinimumVersion);
    }

    private static FakeToolCaller CreateCaller(IReadOnlyList<McpRemoteTool> tools) => new(McpProtocolVersions.July2026, tools, JsonSerializer.SerializeToElement(new WeatherResponse("sunny")));
    private static McpRemoteTool WeatherRemoteTool() => new(new McpToolName("weather.get"), new ToolVersion("2.1"));
    private static Task<WeatherResponse> Forward(WeatherTools tools, WeatherRequest request) => tools.GetAsync(request);
    private abstract class WeatherTools
    {
        [McpTool("weather.get", "2.1")]
        public abstract Task<WeatherResponse> GetAsync(WeatherRequest request, CancellationToken cancellationToken = default);
    }

    private abstract class ValueTaskTools
    {
        [McpTool("weather.value-task", "1")]
        public abstract ValueTask<WeatherResponse> GetAsync(WeatherRequest request, CancellationToken cancellationToken = default);
    }

    private abstract class PropertyTools
    {
        public Task<WeatherResponse> Pending { get; } = Task.FromResult(new WeatherResponse("pending"));

        [McpTool("property.execute", "1")]
        public abstract Task<WeatherResponse> ExecuteAsync(WeatherRequest request);
    }

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

    private sealed record RequestHolder(WeatherRequest Request);
    private sealed record WeatherRequest(string City);
    private sealed record WeatherResponse(string Forecast);
    private sealed class FakeToolCaller(McpProtocolVersion protocolVersion, IReadOnlyList<McpRemoteTool> tools, JsonElement response): IMcpToolCaller
    {
        public Func<McpToolMethodDescriptor, object, JsonSerializerOptions, CancellationToken, ValueTask<JsonElement>>? CallHandler { get; set; }
        public int CallCount { get; private set; }
        public int DisposeCount { get; private set; }
        public bool IsDisposed => DisposeCount != 0;
        public int ListCount { get; private set; }
        public Func<CancellationToken, ValueTask<IReadOnlyList<McpRemoteTool>>>? ListToolsHandler { get; set; }
        public McpToolMethodDescriptor? LastMethod { get; private set; }
        public object? LastRequest { get; private set; }
        public JsonSerializerOptions? LastSerializerOptions { get; private set; }
        public JsonElement Response { get; set; } = response;
        public IReadOnlyList<McpRemoteTool> Tools { get; set; } = tools;
        public McpProtocolVersion ProtocolVersion => protocolVersion;

        public ValueTask<JsonElement> CallAsync(McpToolMethodDescriptor method, object request, JsonSerializerOptions serializerOptions, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastMethod = method;
            LastRequest = request;
            LastSerializerOptions = serializerOptions;
            return CallHandler?.Invoke(method, request, serializerOptions, cancellationToken) ?? ValueTask.FromResult(Response);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<McpRemoteTool>> ListToolsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ListCount++;
            return ListToolsHandler?.Invoke(cancellationToken) ?? ValueTask.FromResult(Tools);
        }
    }
}
