// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class ToolCatalogCoordinatorTests
{
    [Fact]
    public void Constructor_WhenDependencyNull_RejectsBeforeObservationOrDiscovery()
    {
        var discovery = new ToolCatalogDiscovery(new CallbackToolRegistrationCatalog(), TimeProvider.System,
            NullLogger<ToolCatalogDiscovery>.Instance, NullLogger<ToolDiscoveryCapture>.Instance, NullLogger<ToolCatalogCapture>.Instance);
        var merger = new ToolCatalogMerger(new CallbackToolCatalogMergePolicy(), TimeProvider.System, NullLogger<ToolCatalogMerger>.Instance);
        var services = new ServiceCollection();
        _ = services.AddToolSchemaEngine();
        using var host = services.BuildServiceProvider();
        var engine = host.GetRequiredService<IToolSchemaEngine>();
        var limits = ToolSchemaTestData.Limits;
        var versions = new GuidIdentifierGenerator<ToolCatalogVersion>(static value => new ToolCatalogVersion(value.ToString()));
        var logger = NullLogger<ToolCatalogCoordinator>.Instance;
        Should.Throw<ArgumentNullException>(() => new ToolCatalogCoordinator(null!, merger, engine, limits, versions, TimeProvider.System, logger)).ParamName.ShouldBe("discovery");
        Should.Throw<ArgumentNullException>(() => new ToolCatalogCoordinator(discovery, null!, engine, limits, versions, TimeProvider.System, logger)).ParamName.ShouldBe("merger");
        Should.Throw<ArgumentNullException>(() => new ToolCatalogCoordinator(discovery, merger, null!, limits, versions, TimeProvider.System, logger)).ParamName.ShouldBe("schemaEngine");
        Should.Throw<ArgumentNullException>(() => new ToolCatalogCoordinator(discovery, merger, engine, null!, versions, TimeProvider.System, logger)).ParamName.ShouldBe("schemaLimits");
        Should.Throw<ArgumentNullException>(() => new ToolCatalogCoordinator(discovery, merger, engine, limits, null!, TimeProvider.System, logger)).ParamName.ShouldBe("versions");
        Should.Throw<ArgumentNullException>(() => new ToolCatalogCoordinator(discovery, merger, engine, limits, versions, null!, logger)).ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new ToolCatalogCoordinator(discovery, merger, engine, limits, versions, TimeProvider.System, null!)).ParamName.ShouldBe("logger");
    }

    [Fact]
    public async Task CaptureAsync_WhenRequestNull_RejectsBeforeDiscoveryOrObservation()
    {
        var (host, coordinator, _) = Compose([], []);
        await using var owningHost = host;
        (await Should.ThrowAsync<ArgumentNullException>(async () => await coordinator.CaptureAsync(null!, TestContext.Current.CancellationToken))).ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task CaptureAsync_WhenMergeAndPreflightSucceed_TransfersSoleSourceOwnershipToReturnedCatalog()
    {
        var candidate = ToolCatalogMergeTestData.Candidate();
        var invoker = new CaptureTestToolInvoker();
        var releases = 0;
        var source = new CallbackToolProviderCapture(candidate.Source)
        {
            Acquire = (_, _) => ValueTask.FromResult<ToolInvokerLeaseResult>(new ToolInvokerAcquired(new ToolInvokerLease(
                candidate.Tool, candidate.Source.SourceVersion, invoker, () => { releases++; return ValueTask.CompletedTask; }))),
        };
        var (host, coordinator, request) = Compose([candidate.Toolset], [Provider(candidate.Source, source)]);
        await using var owningHost = host;

        var result = await coordinator.CaptureAsync(request, TestContext.Current.CancellationToken);

        var captured = result.ShouldBeOfType<ToolCatalogCoordinatorCaptured>();
        source.Disposals.ShouldBe(0);
        var lease = (await captured.Catalog.AcquireInvokerAsync(candidate.Identity, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;
        lease.Invoker.ShouldBeSameAs(invoker);
        var closing = captured.Catalog.DisposeAsync().AsTask();
        closing.IsCompleted.ShouldBeFalse();
        await lease.DisposeAsync();
        await closing;
        source.Disposals.ShouldBe(1);
        releases.ShouldBe(1);
    }

    [Fact]
    public async Task CaptureAsync_WhenMergeRejects_ReleasesEveryDiscoveredSourceAndReturnsRejection()
    {
        var first = ToolCatalogMergeTestData.Candidate("first", "source.first");
        var second = ToolCatalogMergeTestData.Candidate("second", "source.second");
        var a = new CallbackToolProviderCapture(first.Source);
        var b = new CallbackToolProviderCapture(second.Source);
        var (host, coordinator, request) = Compose([first.Toolset, second.Toolset], [Provider(first.Source, a), Provider(second.Source, b)]);
        await using var owningHost = host;

        var result = await coordinator.CaptureAsync(request, TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<ToolCatalogCoordinatorMergeRejected>();
        rejected.Context.Collisions.ShouldNotBeEmpty();
        a.Disposals.ShouldBe(1);
        b.Disposals.ShouldBe(1);
        a.Acquisitions.ShouldBe(0);
        b.Acquisitions.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CaptureAsync_WhenSchemaPreflightRejects_ReleasesEveryDiscoveredSourceAndReturnsRejection(bool rejectOutput)
    {
        var goodSchema = ToolSchemaTestData.Schema("{}");
        var badSchema = ToolSchemaTestData.Schema(/*lang=json,strict*/ """{"items":1}""");
        var tool = new ToolDescriptor(new ToolId("tool.malformed"), new ToolVersion("1"), "malformed", "Malformed schema tool",
            rejectOutput ? goodSchema : badSchema, rejectOutput ? badSchema : null,
            new ToolEffects(ToolEffect.ReadOnly, null, null), new ToolExecutionHints(ToolSchedulingMode.Unspecified, null, null, null),
            new ToolSourceId("source.malformed"), ExtensionData.Empty);
        var source = ToolCatalogMergeTestData.Source("source.malformed", [tool]);
        var toolset = ToolCatalogMergeTestData.Toolset("malformed", [source], [ToolCatalogMergeTestData.Alias("malformed", tool)]);
        var capture = new CallbackToolProviderCapture(source);
        var (host, coordinator, request) = Compose([toolset], [Provider(source, capture)]);
        await using var owningHost = host;

        var result = await coordinator.CaptureAsync(request, TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<ToolCatalogCoordinatorSchemaRejected>();
        rejected.Tool.ShouldBe(tool);
        rejected.IsOutputSchema.ShouldBe(rejectOutput);
        rejected.Rejection.Reason.ShouldBe(ToolSchemaRejectionReason.InvalidSchema);
        capture.Disposals.ShouldBe(1);
        capture.Acquisitions.ShouldBe(0);
    }

    [Fact]
    public async Task CaptureAsync_WhenCancelledDuringMerge_ReleasesEveryDiscoveredSourceAndPropagatesCancellation()
    {
        var candidate = ToolCatalogMergeTestData.Candidate();
        var source = new CallbackToolProviderCapture(candidate.Source);
        using var cancellation = new CancellationTokenSource();
        var policy = new CallbackToolCatalogMergePolicy
        {
            Resolve = (context, _) =>
            {
                cancellation.Cancel();
                return ValueTask.FromResult<ToolCatalogMergeDecision>(new ToolCatalogSelection(context.Candidates,
                    ImmutableDictionary<ToolAlias, ToolCatalogCandidate>.Empty.Add(new ToolAlias("read"), context.Candidates[0])));
            },
        };
        var (host, coordinator, request) = Compose([candidate.Toolset], [Provider(candidate.Source, source)], policy);
        await using var owningHost = host;

        var failure = await Should.ThrowAsync<OperationCanceledException>(
            async () => await coordinator.CaptureAsync(request, cancellation.Token));

        failure.CancellationToken.ShouldBe(cancellation.Token);
        source.Disposals.ShouldBe(1);
        source.Acquisitions.ShouldBe(0);
    }

    [Fact]
    public async Task CaptureAsync_WhenCancelledAfterPreflightBeforeTransfer_ReleasesEveryDiscoveredSourceAndPropagatesCancellation()
    {
        var candidate = ToolCatalogMergeTestData.Candidate();
        var source = new CallbackToolProviderCapture(candidate.Source);
        using var cancellation = new CancellationTokenSource();
        var services = new ServiceCollection();
        _ = services.AddToolset(candidate.Toolset);
        _ = services.AddToolProvider(candidate.Source.SourceId, Provider(candidate.Source, source));
        _ = services.AddToolCatalogCoordinator();
        using var realHost = new ServiceCollection().AddToolSchemaEngine().BuildServiceProvider();
        var realEngine = realHost.GetRequiredService<IToolSchemaEngine>();
        var scriptedEngine = new CallbackToolSchemaEngine
        {
            OnCompile = (schema, limits, token) =>
            {
                var result = realEngine.Compile(schema, limits, token);
                cancellation.Cancel();
                return result;
            },
        };
        _ = services.AddSingleton<IToolSchemaEngine>(scriptedEngine);
        await using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var coordinator = host.GetRequiredService<ToolCatalogCoordinator>();
        var request = ToolCatalogMergeTestData.Request([candidate.Toolset]);

        var failure = await Should.ThrowAsync<OperationCanceledException>(
            async () => await coordinator.CaptureAsync(request, cancellation.Token));

        failure.CancellationToken.ShouldBe(cancellation.Token);
        source.Disposals.ShouldBe(1);
        source.Acquisitions.ShouldBe(0);
    }

    [Theory]
    [InlineData("captured")]
    [InlineData("merge_rejected")]
    [InlineData("schema_rejected")]
    [InlineData("cancelled")]
    public async Task CaptureAsync_WhenObserved_EmitsCorrelatedTerminalSignalsWithoutPublicationOrSchemaContent(string outcome)
    {
        const string secret = "private-publication-or-schema-content";
        var badSchema = ToolSchemaTestData.Schema(/*lang=json,strict*/ """{"items":1,"description":"private-publication-or-schema-content"}""");
        var goodSchema = ToolSchemaTestData.Schema("{}");
        var tool = new ToolDescriptor(new ToolId("tool.observed"), new ToolVersion("1"), "observed", secret,
            outcome == "schema_rejected" ? badSchema : goodSchema, null, new ToolEffects(ToolEffect.ReadOnly, null, null),
            new ToolExecutionHints(ToolSchedulingMode.Unspecified, null, null, null), new ToolSourceId("source.observed"), ExtensionData.Empty);
        var source = ToolCatalogMergeTestData.Source("source.observed", [tool]);
        var toolset = ToolCatalogMergeTestData.Toolset("observed", [source], [ToolCatalogMergeTestData.Alias(secret, tool)]);
        var capture = new CallbackToolProviderCapture(source);
        IToolCatalogMergePolicy? policy = null;
        using var cancellation = new CancellationTokenSource();
        if (outcome == "merge_rejected")
        {
            policy = new CallbackToolCatalogMergePolicy { Resolve = static (_, _) => ValueTask.FromResult<ToolCatalogMergeDecision>(new ToolCatalogRejection()) };
        }
        if (outcome == "cancelled") { cancellation.Cancel(); }

        var logger = new RecordingLogger<ToolCatalogCoordinator>();
        var services = new ServiceCollection();
        _ = services.AddToolset(toolset);
        _ = services.AddToolProvider(source.SourceId, Provider(source, capture));
        if (policy is not null) { _ = services.AddSingleton(policy); }
        _ = services.AddToolCatalogCoordinator();
        _ = services.AddSingleton<ILogger<ToolCatalogCoordinator>>(logger);
        await using var observedHost = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var observedCoordinator = observedHost.GetRequiredService<ToolCatalogCoordinator>();
        var request = ToolCatalogMergeTestData.Request([toolset]);

        using var parent = new Activity("coordinate-catalog-test").Start();
        Activity? observed = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static candidate => candidate.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { if (activity.TraceId == parent.TraceId && activity.OperationName == AgentKitActivityNames.ToolCatalogCoordinate) { observed = activity; } },
        };
        ActivitySource.AddActivityListener(listener);
        if (outcome is "captured" or "merge_rejected" or "schema_rejected")
        {
            var result = await observedCoordinator.CaptureAsync(request, TestContext.Current.CancellationToken);
            if (result is ToolCatalogCoordinatorCaptured success) { await success.Catalog.DisposeAsync(); }
        }
        else
        {
            _ = await Should.ThrowAsync<OperationCanceledException>(async () => await observedCoordinator.CaptureAsync(request, cancellation.Token));
        }

        var activity = observed.ShouldNotBeNull();
        activity.ParentId.ShouldBe(parent.Id);
        activity.Status.ShouldBe(outcome == "captured" ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe(outcome);
        activity.TagObjects.ShouldAllBe(tag => tag.Value == null || !tag.Value.ToString()!.Contains(secret, StringComparison.Ordinal));
        var logs = logger.Snapshot();
        logs.Select(static entry => entry.EventId.Id).ShouldBe([4100, 4101]);
        logs.ShouldAllBe(entry => entry.Category == typeof(ToolCatalogCoordinator).FullName);
        logs.ShouldAllBe(entry => !entry.Message.Contains(secret, StringComparison.Ordinal));
        logs[1].State["Outcome"].ShouldBe(outcome);
        Activity.Current.ShouldBeSameAs(parent);
    }

    private static CallbackToolProvider Provider(ToolProviderSnapshot snapshot, IToolProviderCapture capture) => new(snapshot.SourceId)
    {
        Discover = (_, _) => ValueTask.FromResult(capture),
    };

    private static (ServiceProvider Host, ToolCatalogCoordinator Coordinator, ToolDiscoveryRequest Request) Compose(
        ImmutableArray<ToolsetPublication> toolsets, ImmutableArray<IToolProvider> providers, IToolCatalogMergePolicy? policy = null)
    {
        var services = new ServiceCollection();
        foreach (var toolset in toolsets) { _ = services.AddToolset(toolset); }
        foreach (var provider in providers) { _ = services.AddToolProvider(provider.SourceId, provider); }
        if (policy is not null) { _ = services.AddSingleton(policy); }
        _ = services.AddToolCatalogCoordinator();
        var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        return (host, host.GetRequiredService<ToolCatalogCoordinator>(), ToolCatalogMergeTestData.Request(toolsets));
    }
}
