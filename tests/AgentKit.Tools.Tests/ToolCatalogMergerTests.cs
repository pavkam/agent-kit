// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class ToolCatalogMergerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MergeAsync_WhenSelectionHasNoTools_PublishesEmptyCatalogWithEverySelectedSourceVersion(bool selectEmptySource)
    {
        var source = ToolCatalogMergeTestData.Source("empty", []);
        ImmutableArray<ToolsetPublication> toolsets = selectEmptySource ? [ToolCatalogMergeTestData.Toolset("empty", [source], [])] : [];
        var sources = selectEmptySource ? ImmutableDictionary<ToolSourceId, ToolProviderSnapshot>.Empty.Add(source.SourceId, source) : [];
        var services = new ServiceCollection();
        _ = services.AddToolCatalogMerging();
        await using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var (snapshot, context) = await host.GetRequiredService<ToolCatalogMerger>().MergeAsync(ToolCatalogMergeTestData.Request(toolsets), new ToolCatalogVersion("empty-catalog"), toolsets, sources, TestContext.Current.CancellationToken);
        var catalog = snapshot.ShouldNotBeNull();
        catalog.Tools.ShouldBeEmpty();
        catalog.ProviderAliases.ShouldBeEmpty();
        catalog.ExecutionPolicies.ShouldBeEmpty();
        catalog.SourceVersions.Count.ShouldBe(selectEmptySource ? 1 : 0);
        if (selectEmptySource) { catalog.SourceVersions[source.SourceId].ShouldBe(source.SourceVersion); }
        context.Candidates.ShouldBeEmpty();
        context.Collisions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("selected")]
    [InlineData("rejected")]
    [InlineData("cancelled")]
    [InlineData("failed")]
    public async Task MergeAsync_WhenObserved_EmitsCorrelatedTerminalSignalsWithoutPublicationOrExceptionContent(string outcome)
    {
        const string secret = "private-publication-or-exception-content";
        var candidate = ToolCatalogMergeTestData.Candidate(alias: secret);
        var request = ToolCatalogMergeTestData.Request([candidate.Toolset]);
        var policy = new CallbackToolCatalogMergePolicy
        {
            Resolve = (context, _) => outcome switch
            {
                "failed" => throw new InvalidOperationException(secret),
                "rejected" => ValueTask.FromResult<ToolCatalogMergeDecision>(new ToolCatalogRejection()),
                _ => ValueTask.FromResult<ToolCatalogMergeDecision>(new ToolCatalogSelection(context.Candidates, ImmutableDictionary<ToolAlias, ToolCatalogCandidate>.Empty.Add(new ToolAlias(secret), context.Candidates[0]))),
            },
        };
        using var parent = new Activity("observed-catalog-merge").Start();
        Activity? observed = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { if (activity.TraceId == parent.TraceId) { observed = activity; } },
        };
        ActivitySource.AddActivityListener(listener);
        var logger = new RecordingLogger<ToolCatalogMerger>();
        var merger = new ToolCatalogMerger(policy, TimeProvider.System, logger);
        using var cancellation = new CancellationTokenSource();
        if (outcome == "cancelled") { cancellation.Cancel(); }
        var pending = merger.MergeAsync(request, new ToolCatalogVersion("catalog"), [candidate.Toolset], ImmutableDictionary<ToolSourceId, ToolProviderSnapshot>.Empty.Add(candidate.Source.SourceId, candidate.Source), cancellation.Token).AsTask();
        if (outcome == "cancelled") { _ = await Should.ThrowAsync<OperationCanceledException>(() => pending); }
        else if (outcome == "failed") { _ = await Should.ThrowAsync<InvalidOperationException>(() => pending); }
        else { _ = await pending; }
        var activity = observed.ShouldNotBeNull();
        activity.ParentId.ShouldBe(parent.Id);
        activity.OperationName.ShouldBe(AgentKitActivityNames.ToolCatalogMerge);
        activity.Status.ShouldBe(outcome == "selected" ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe(outcome);
        activity.GetTagItem(AgentKitTagNames.RunId).ShouldBe(request.RunId.ToString());
        activity.TagObjects.ShouldAllBe(tag => tag.Value == null || !tag.Value.ToString()!.Contains(secret, StringComparison.Ordinal));
        var logs = logger.Snapshot();
        logs.Select(static entry => entry.EventId.Id).ShouldBe([4060, 4061]);
        logs.ShouldAllBe(entry => entry.Category == typeof(ToolCatalogMerger).FullName);
        logs.ShouldAllBe(entry => !entry.Message.Contains(secret, StringComparison.Ordinal));
        logs[1].State["Outcome"].ShouldBe(outcome);
        Activity.Current.ShouldBeSameAs(parent);
    }

    [Fact]
    public async Task MergeAsync_WhenPolicyChoosesConfiguredSource_OnlySelectedRetainedInvokerCanBeAcquired()
    {
        var first = ToolCatalogMergeTestData.Candidate("first", "source.first");
        var second = ToolCatalogMergeTestData.Candidate("second", "source.second");
        var firstInvoker = new CaptureTestToolInvoker();
        var secondInvoker = new CaptureTestToolInvoker();
        var calls = 0;
        var policy = new CallbackToolCatalogMergePolicy
        {
            Resolve = (context, _) =>
            {
                calls++;
                context.Collisions.Length.ShouldBe(2);
                var selected = context.Candidates.Single(candidate => candidate.Source.SourceId == second.Source.SourceId);
                return ValueTask.FromResult<ToolCatalogMergeDecision>(new ToolCatalogSelection([selected], ImmutableDictionary<ToolAlias, ToolCatalogCandidate>.Empty.Add(new ToolAlias("read"), selected)));
            },
        };
        var services = new ServiceCollection();
        _ = services.AddStaticToolProvider(first.Source, ToolCaptureTestData.Bindings(first.Tool, firstInvoker));
        _ = services.AddStaticToolProvider(second.Source, ToolCaptureTestData.Bindings(second.Tool, secondInvoker));
        _ = services.AddSingleton<IToolCatalogMergePolicy>(policy);
        _ = services.AddToolCatalogMerging();
        await using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var request = ToolCatalogMergeTestData.Request([first.Toolset, second.Toolset]);
        await using var firstCapture = await host.GetRequiredKeyedService<IToolProvider>(first.Source.SourceId).DiscoverAsync(request, TestContext.Current.CancellationToken);
        await using var secondCapture = await host.GetRequiredKeyedService<IToolProvider>(second.Source.SourceId).DiscoverAsync(request, TestContext.Current.CancellationToken);
        var (snapshot, _) = await host.GetRequiredService<ToolCatalogMerger>().MergeAsync(request, new ToolCatalogVersion("catalog"), [first.Toolset, second.Toolset],
            ImmutableDictionary<ToolSourceId, ToolProviderSnapshot>.Empty.Add(first.Source.SourceId, firstCapture.Snapshot).Add(second.Source.SourceId, secondCapture.Snapshot), TestContext.Current.CancellationToken);
        calls.ShouldBe(1);
        var catalog = new ToolCatalogCapture(snapshot.ShouldNotBeNull(), ImmutableDictionary<ToolSourceId, IToolProviderCapture>.Empty.Add(first.Source.SourceId, firstCapture).Add(second.Source.SourceId, secondCapture),
            TimeProvider.System, NullLogger<ToolCatalogCapture>.Instance);
        var acquired = (await catalog.AcquireInvokerAsync(second.Identity, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>();
        acquired.Lease.Invoker.ShouldBeSameAs(secondInvoker);
        acquired.Lease.Tool.ShouldBe(second.Tool);
        await acquired.Lease.DisposeAsync();
        await catalog.DisposeAsync();
        firstInvoker.Invocations.ShouldBe(0);
        secondInvoker.Invocations.ShouldBe(0);
        firstInvoker.Disposals.ShouldBe(0);
        secondInvoker.Disposals.ShouldBe(0);
    }

    [Fact]
    public async Task MergeAsync_WhenDefaultPolicyRejects_ReturnsCompleteCollisionEvidenceWithoutSnapshot()
    {
        var first = ToolCatalogMergeTestData.Candidate("first", "source.first");
        var second = ToolCatalogMergeTestData.Candidate("second", "source.second");
        var services = new ServiceCollection();
        _ = services.AddToolCatalogMerging();
        await using var host = services.BuildServiceProvider();
        var (snapshot, context) = await host.GetRequiredService<ToolCatalogMerger>().MergeAsync(ToolCatalogMergeTestData.Request([first.Toolset, second.Toolset]), new ToolCatalogVersion("catalog"),
            [first.Toolset, second.Toolset], ImmutableDictionary<ToolSourceId, ToolProviderSnapshot>.Empty.Add(first.Source.SourceId, first.Source).Add(second.Source.SourceId, second.Source), TestContext.Current.CancellationToken);
        snapshot.ShouldBeNull();
        context.Candidates.ShouldBe([first, second]);
        context.Collisions.Length.ShouldBe(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MergeAsync_WhenPolicyCompletesAfterCancellation_TransfersNoSnapshot(bool reject)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ToolCatalogMergeDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken received = default;
        var policy = new CallbackToolCatalogMergePolicy { Resolve = (_, token) => { received = token; entered.SetResult(); return new(release.Task); } };
        var merger = Create(policy);
        using var cancellation = new CancellationTokenSource();
        var pending = merger.MergeAsync(ToolCaptureTestData.Discovery(), new ToolCatalogVersion("catalog"), [], [], cancellation.Token).AsTask();
        await entered.Task;
        pending.IsCompleted.ShouldBeFalse();
        cancellation.Cancel();
        release.SetResult(reject ? new ToolCatalogRejection() : new ToolCatalogSelection([], []));
        var error = await Should.ThrowAsync<OperationCanceledException>(() => pending);
        error.CancellationToken.ShouldBe(cancellation.Token);
        received.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task MergeAsync_WhenCancelledBeforePolicy_DoesNotCallPolicy()
    {
        var calls = 0;
        var policy = new CallbackToolCatalogMergePolicy { Resolve = (_, _) => { calls++; return ValueTask.FromResult<ToolCatalogMergeDecision>(new ToolCatalogRejection()); } };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var exception = await Should.ThrowAsync<OperationCanceledException>(async () => await Create(policy).MergeAsync(ToolCaptureTestData.Discovery(), new ToolCatalogVersion("catalog"), [], [], cancellation.Token));
        exception.CancellationToken.ShouldBe(cancellation.Token);
        calls.ShouldBe(0);
    }

    [Fact]
    public async Task MergeAsync_WhenPolicyFailsOrReturnsNull_PropagatesFailureWithoutFabricatingRejection()
    {
        var failure = new InvalidOperationException("policy-sensitive-content");
        var policy = new CallbackToolCatalogMergePolicy { Resolve = (_, _) => throw failure };
        var merger = Create(policy);
        (await Should.ThrowAsync<InvalidOperationException>(async () => await merger.MergeAsync(ToolCaptureTestData.Discovery(), new ToolCatalogVersion("catalog"), [], [], TestContext.Current.CancellationToken))).ShouldBeSameAs(failure);
        policy.Resolve = (_, _) => ValueTask.FromResult<ToolCatalogMergeDecision>(null!);
        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await merger.MergeAsync(ToolCaptureTestData.Discovery(), new ToolCatalogVersion("catalog"), [], [], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MergeAsync_WhenInputInvalid_RejectsBeforeAnyCallback()
    {
        var reads = 0;
        var calls = 0;
        var policy = new CallbackToolCatalogMergePolicy { Resolve = (_, _) => { calls++; return ValueTask.FromResult<ToolCatalogMergeDecision>(new ToolCatalogRejection()); } };
        var clock = new CallbackTimestampTimeProvider(() => ++reads);
        var logger = new RecordingLogger<ToolCatalogMerger>();
        Should.Throw<ArgumentNullException>(() => new ToolCatalogMerger(null!, clock, logger)).ParamName.ShouldBe("policy");
        Should.Throw<ArgumentNullException>(() => new ToolCatalogMerger(policy, null!, logger)).ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new ToolCatalogMerger(policy, clock, null!)).ParamName.ShouldBe("logger");
        var merger = new ToolCatalogMerger(policy, clock, logger);
        (await Should.ThrowAsync<ArgumentNullException>(async () => await merger.MergeAsync(null!, new ToolCatalogVersion("catalog"), [], [], TestContext.Current.CancellationToken))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await merger.MergeAsync(ToolCaptureTestData.Discovery(), default, [], [], TestContext.Current.CancellationToken))).ParamName.ShouldBe("version");
        (await Should.ThrowAsync<ArgumentException>(async () => await merger.MergeAsync(ToolCaptureTestData.Discovery(), new ToolCatalogVersion("catalog"), default, [], TestContext.Current.CancellationToken))).ParamName.ShouldBe("toolsets");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await merger.MergeAsync(ToolCaptureTestData.Discovery(), new ToolCatalogVersion("catalog"), [], null!, TestContext.Current.CancellationToken))).ParamName.ShouldBe("sources");
        reads.ShouldBe(0);
        calls.ShouldBe(0);
        logger.Snapshot().ShouldBeEmpty();
    }

    private static ToolCatalogMerger Create(IToolCatalogMergePolicy policy) => new(policy, TimeProvider.System, NullLogger<ToolCatalogMerger>.Instance);
}
