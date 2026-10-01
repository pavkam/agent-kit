// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

using System.Diagnostics;

using AgentKit.Observability;

public sealed partial class ArtifactCoordinatorTests
{
    [Fact]
    public async Task PrepareAsync_WhenObserved_EmitsACorrelatedActivityLogAndBoundedMetrics()
    {
        var logger = new RecordingLogger<ArtifactCoordinator>();
        using var harness = CoordinatorHarness.Create(extra: services => _ = services.AddSingleton<ILogger<ArtifactCoordinator>>(logger));
        using var activities = Collect(AgentKitActivityNames.ArtifactPrepare);
        using var metrics = new MetricCollector(AgentKitMetricNames.ArtifactOperationCount);
        using var durations = new MetricCollector(AgentKitMetricNames.ArtifactOperationDuration);

        var prepared = (await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content), TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactPrepared>();

        _ = prepared;
        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Ok);
        span.GetTagItem(AgentKitTagNames.ArtifactCoordinatorKey).ShouldBe("coordinator");
        span.GetTagItem(AgentKitTagNames.ArtifactOperation).ShouldBe("prepare");
        span.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("prepared");
        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(29000);
        entry.Level.ShouldBe(LogLevel.Debug);
        metrics.Snapshot().ShouldContain(static measurement =>
            measurement.Tags[AgentKitTagNames.ArtifactOperation]!.Equals("prepare") && measurement.Tags[AgentKitTagNames.Outcome]!.Equals("prepared"));
        metrics.Snapshot().ShouldAllBe(static measurement => measurement.Tags.Keys.Order().SequenceEqual(
            new[] { AgentKitTagNames.ArtifactOperation, AgentKitTagNames.Outcome }.Order()));
        durations.Snapshot().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task FinalizeAsync_WhenObserved_TagsThePreparationAndNeverExposesProtectedContent()
    {
        var logger = new RecordingLogger<ArtifactCoordinator>();
        using var harness = CoordinatorHarness.Create(extra: services => _ = services.AddSingleton<ILogger<ArtifactCoordinator>>(logger));
        var prepared = (await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content, key: "secret-replay-key"), TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactPrepared>();
        using var activities = Collect(AgentKitActivityNames.ArtifactFinalize);
        using var metrics = new MetricCollector(AgentKitMetricNames.ArtifactOperationCount);

        _ = await harness.Coordinator.FinalizeAsync(ArtifactTestData.Finalize(prepared.PreparationId), TestContext.Current.CancellationToken);

        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.GetTagItem(AgentKitTagNames.ArtifactPreparationId).ShouldBe(prepared.PreparationId.ToString());
        span.GetTagItem(AgentKitTagNames.TenantId).ShouldBe(ArtifactTestData.Identity.TenantId.Value);
        SignalAssertions.ShouldNotContainContent(
            activities.Snapshot(), logger.Snapshot(), metrics.Snapshot(), "complete output", "text/plain", "session:owner", "secret-replay-key");
    }

    [Theory]
    [InlineData("read")]
    [InlineData("delete")]
    public async Task ReadAndDeleteAsync_WhenObserved_TagTheArtifact(string operation)
    {
        using var harness = CoordinatorHarness.Create();
        var reference = await harness.CommitAsync(_content);
        using var activities = Collect(operation == "read" ? AgentKitActivityNames.ArtifactRead : AgentKitActivityNames.ArtifactDelete);

        if (operation == "read")
        {
            await using var opened = (await harness.Coordinator.ReadAsync(ArtifactTestData.Read(reference), TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactReadOpened>();
        }
        else
        {
            _ = await harness.Coordinator.DeleteAsync(ArtifactTestData.Delete(reference), TestContext.Current.CancellationToken);
        }

        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.GetTagItem(AgentKitTagNames.ArtifactId).ShouldBe(reference.Id.ToString());
        span.Status.ShouldBe(ActivityStatusCode.Ok);
    }

    [Fact]
    public async Task AbortAndReconcileAsync_WhenObserved_UseTheirOwnActivityNames()
    {
        using var harness = CoordinatorHarness.Create(withIntents: true);
        var (preparation, _) = await PrepareAsync(harness);
        using var aborts = Collect(AgentKitActivityNames.ArtifactAbort);
        using var reconciles = Collect(AgentKitActivityNames.ArtifactReconcile);
        _ = await harness.Coordinator.AbortAsync(ArtifactTestData.Abort(preparation), TestContext.Current.CancellationToken);

        _ = await harness.Coordinator.ReconcileAsync(ArtifactTestData.Reconcile(preparation), TestContext.Current.CancellationToken);

        aborts.Snapshot().ShouldNotBeEmpty();
        var reconcile = reconciles.Snapshot().ShouldHaveSingleItem();
        reconcile.GetTagItem(AgentKitTagNames.ArtifactOperation).ShouldBe("reconcile");
        reconcile.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("pending");
        reconcile.Status.ShouldBe(ActivityStatusCode.Ok);
    }

    [Fact]
    public async Task PrepareAsync_WhenRejected_MarksTheActivityFailedWithTheBoundedOutcome()
    {
        using var harness = CoordinatorHarness.Create();
        harness.Authority.Deny = true;
        using var activities = Collect(AgentKitActivityNames.ArtifactPrepare);

        _ = await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content), TestContext.Current.CancellationToken);

        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("denied");
    }

    [Fact]
    public async Task PrepareAsync_WhenCancelled_RecordsACancelledOutcomeAndRethrows()
    {
        var logger = new RecordingLogger<ArtifactCoordinator>();
        using var harness = CoordinatorHarness.Create(extra: services => _ = services.AddSingleton<ILogger<ArtifactCoordinator>>(logger));
        using var activities = Collect(AgentKitActivityNames.ArtifactPrepare);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content), cancelled.Token));

        activities.Snapshot().ShouldHaveSingleItem().GetTagItem(AgentKitTagNames.Outcome).ShouldBe("cancelled");
        logger.Snapshot().ShouldHaveSingleItem().EventId.Id.ShouldBe(29002);
    }

    [Fact]
    public async Task PrepareAsync_WhenTheSecurityAuthorityFaults_RecordsAFaultedOutcomeAndRethrows()
    {
        var logger = new RecordingLogger<ArtifactCoordinator>();
        using var harness = CoordinatorHarness.Create(extra: services => _ = services.AddSingleton<ILogger<ArtifactCoordinator>>(logger));
        harness.Authority.OnAuthorize = static _ => throw new InvalidOperationException("authority failure");
        using var activities = Collect(AgentKitActivityNames.ArtifactPrepare);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content), TestContext.Current.CancellationToken));

        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("faulted");
        span.Status.ShouldBe(ActivityStatusCode.Error);
        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(29001);
        entry.Level.ShouldBe(LogLevel.Error);
    }

    [Fact]
    public async Task PrepareAsync_WhenTheLoggerThrows_StillReturnsTheSemanticResult()
    {
        using var harness = CoordinatorHarness.Create(extra: services =>
            _ = services.AddSingleton<ILogger<ArtifactCoordinator>>(new RecordingLogger<ArtifactCoordinator> { ThrowOnWrite = true }));

        var result = await harness.Coordinator.PrepareAsync(ArtifactTestData.Prepare(_content), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ArtifactPrepared>();
    }

    private static ActivityCollector Collect(string operationName) => new(
        static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
        observation => observation.OperationName == operationName
            && observation.GetTagItem(AgentKitTagNames.TenantId)?.ToString() == ArtifactTestData.Identity.TenantId.Value);
}
