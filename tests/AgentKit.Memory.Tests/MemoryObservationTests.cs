// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Tests;

/// <summary>Verifies the runtime's activities, metrics, and logs: names, correlation, terminal status, bounded dimensions, and content absence.</summary>
public sealed class MemoryObservationTests
{
    private static bool Contains(object? value, string text) => value?.ToString()?.Contains(text, StringComparison.Ordinal) == true;

    private static bool HasOutcome(MetricObservation measurement, string outcome) =>
        measurement.Tags.TryGetValue(AgentKitTagNames.Outcome, out var value) && Equals(value, outcome);

    private static ActivityCollector Collect(string name, string tenant) => new(
        static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
        observation => observation.OperationName == name && observation.GetTagItem(AgentKitTagNames.TenantId)?.ToString() == tenant);

    [Fact]
    public async Task ProposeAsync_WhenAccepted_RecordsASuccessfulActivityAndBoundedMetrics()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner($"tenant-{Guid.NewGuid():N}");
        using var activities = Collect(AgentKitActivityNames.MemoryPropose, owner.Identity.TenantId.Value);
        using var counts = new MetricCollector(AgentKitMetricNames.MemoryOperationCount);
        var proposal = MemoryTestData.Proposal(owner);

        _ = await harness.Coordinator.ProposeAsync(proposal, hooks: null, TestContext.Current.CancellationToken);

        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Ok);
        span.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("completed");
        span.GetTagItem(AgentKitTagNames.MemoryId).ShouldBe(proposal.Id.ToString());
        span.GetTagItem(AgentKitTagNames.MemoryProfileKey).ShouldBe(MemoryTestData.ProfileKey.Value);
        counts.Snapshot().ShouldContain(static measurement => measurement.Tags.ContainsKey(AgentKitTagNames.Outcome) && measurement.Tags[AgentKitTagNames.Outcome]!.Equals("completed"));
        counts.Snapshot().ShouldAllBe(static measurement => !measurement.Tags.ContainsKey(AgentKitTagNames.MemoryId) && !measurement.Tags.ContainsKey(AgentKitTagNames.TenantId));
    }

    [Fact]
    public async Task ProposeAsync_WhenPolicyDenies_RecordsAnErrorActivityWithThePolicyDeniedOutcome()
    {
        using var harness = MemoryHarness.Create(allowPolicy: false);
        var owner = MemoryTestData.NewOwner($"tenant-{Guid.NewGuid():N}");
        using var activities = Collect(AgentKitActivityNames.MemoryPropose, owner.Identity.TenantId.Value);

        _ = await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(owner), hooks: null, TestContext.Current.CancellationToken);

        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("policy_denied");
    }

    [Fact]
    public async Task ProposeAsync_WhenCancelled_RecordsACancelledOutcomeAndRethrows()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner($"tenant-{Guid.NewGuid():N}");
        using var activities = Collect(AgentKitActivityNames.MemoryPropose, owner.Identity.TenantId.Value);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(owner), hooks: null, source.Token));

        activities.Snapshot().ShouldHaveSingleItem().GetTagItem(AgentKitTagNames.Outcome).ShouldBe("cancelled");
    }

    [Fact]
    public async Task ProposeAsync_WhenTheLoggerThrows_DoesNotChangeTheResult()
    {
        var logger = new RecordingLogger<DefaultMemoryCoordinator> { ThrowOnWrite = true };
        using var harness = MemoryHarness.Create(arrange: services => services.AddSingleton<ILogger<DefaultMemoryCoordinator>>(logger));
        var owner = MemoryTestData.NewOwner($"tenant-{Guid.NewGuid():N}");

        var result = await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(owner), hooks: null, TestContext.Current.CancellationToken);

        result.IsAccepted.ShouldBeTrue();
    }

    [Fact]
    public async Task ProposeAsync_WhenLogged_UsesTheOwnedEventIdAndNeverLogsContent()
    {
        var logger = new RecordingLogger<DefaultMemoryCoordinator>();
        using var harness = MemoryHarness.Create(arrange: services => services.AddSingleton<ILogger<DefaultMemoryCoordinator>>(logger));
        var owner = MemoryTestData.NewOwner($"tenant-{Guid.NewGuid():N}");
        using var activities = Collect(AgentKitActivityNames.MemoryPropose, owner.Identity.TenantId.Value);

        _ = await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(owner, "protected preference body"), hooks: null, TestContext.Current.CancellationToken);

        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(32310);
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Message.ShouldNotContain("protected preference body");
        activities.Snapshot().SelectMany(static span => span.Tags.Values).Any(static value => Contains(value, "protected preference")).ShouldBeFalse();
    }

    [Fact]
    public async Task RetrieveAsync_WhenCompleted_RecordsActivitiesForTheQuerySourceAndMetrics()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner($"tenant-{Guid.NewGuid():N}");
        _ = await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(owner, "The user prefers concise answers."), hooks: null, TestContext.Current.CancellationToken);
        using var retrieval = Collect(AgentKitActivityNames.RetrievalRetrieve, owner.Identity.TenantId.Value);
        using var sourceSpans = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            static observation => observation.OperationName == AgentKitActivityNames.RetrievalSourceSearch
                && observation.GetTagItem(AgentKitTagNames.RetrievalSourceKey)?.ToString() == MemoryRetrievalSourceKeys.DurableMemory.Value);
        using var counts = new MetricCollector(AgentKitMetricNames.RetrievalCount);
        var query = MemoryTestData.Query(owner, "concise answers");

        _ = await harness.Pipeline.RetrieveAsync(query, hooks: null, TestContext.Current.CancellationToken);

        var span = retrieval.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Ok);
        span.GetTagItem(AgentKitTagNames.RetrievalCandidateCount).ShouldBe(1);
        span.GetTagItem(AgentKitTagNames.RetrievalRequestId).ShouldBe(query.Id.ToString());
        sourceSpans.Snapshot().Any(candidate => Contains(candidate.GetTagItem(AgentKitTagNames.RetrievalRequestId), query.Id.ToString())).ShouldBeTrue();
        counts.Snapshot().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task RetrieveAsync_WhenRefused_RecordsAnErrorActivityAndNeverTagsQueryText()
    {
        using var harness = MemoryHarness.Create(profile: configured => configured.MaximumClassification = DataClassification.Internal);
        var owner = MemoryTestData.NewOwner($"tenant-{Guid.NewGuid():N}");
        using var retrieval = Collect(AgentKitActivityNames.RetrievalRetrieve, owner.Identity.TenantId.Value);
        var query = MemoryTestData.Query(owner, "secret query words", maximumClassification: DataClassification.Restricted);

        _ = await harness.Pipeline.RetrieveAsync(query, hooks: null, TestContext.Current.CancellationToken);

        var span = retrieval.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.Tags.Values.Any(static value => Contains(value, "secret query")).ShouldBeFalse();
    }

    [Fact]
    public async Task PublishAsync_WhenCompleted_RecordsADocumentActivityAndMetric()
    {
        using var harness = DocumentHarness.Create();
        var owner = MemoryTestData.NewOwner($"tenant-{Guid.NewGuid():N}");
        using var activities = Collect(AgentKitActivityNames.MemoryDocumentPublish, owner.Identity.TenantId.Value);
        using var counts = new MetricCollector(AgentKitMetricNames.MemoryOperationCount);
        var id = new DocumentId(Guid.NewGuid());

        _ = await harness.Provider.GetRequiredService<IDocumentLifecycleCoordinator>()
            .PublishAsync(DocumentHarness.Publish(owner, id, "v1", "xxx observed text", "p-1"), TestContext.Current.CancellationToken);

        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Ok);
        span.GetTagItem(AgentKitTagNames.MemoryDocumentId).ShouldBe(id.ToString());
        counts.Snapshot().Any(static measurement => HasOutcome(measurement, "completed")).ShouldBeTrue();
    }

    [Fact]
    public async Task RuntimeSelection_WhenTheProfileIsUnknown_RecordsAnErrorActivityAndAWarningLog()
    {
        var logger = new RecordingLogger<DefaultMemoryProfileRuntimeSelector>();
        using var harness = MemoryHarness.Create(arrange: services => services.AddSingleton<ILogger<DefaultMemoryProfileRuntimeSelector>>(logger));
        var owner = MemoryTestData.NewOwner($"tenant-{Guid.NewGuid():N}");
        var unknownProfile = $"missing-{Guid.NewGuid():N}";
        var context = new MemoryOperationContext(
            owner.AgentId, owner.SessionId, owner.Identity, owner.Context.Correlation, owner.Authorization, new MemoryProfileKey(unknownProfile), MemoryTestData.ProfileVersion);
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.MemoryProfileActivate
                && observation.GetTagItem(AgentKitTagNames.MemoryProfileKey)?.ToString() == unknownProfile);

        _ = await harness.Provider.GetRequiredService<IMemoryProfileRuntimeSelector>().SelectAsync(context, TestContext.Current.CancellationToken);

        activities.Snapshot().ShouldHaveSingleItem().Status.ShouldBe(ActivityStatusCode.Error);
        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(32304);
        entry.Level.ShouldBe(LogLevel.Warning);
    }
}
