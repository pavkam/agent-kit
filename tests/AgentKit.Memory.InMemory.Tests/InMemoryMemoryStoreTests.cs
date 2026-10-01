// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.InMemory.Tests;

using System.Diagnostics;

using AgentKit.Observability;

/// <summary>Verifies <see cref="InMemoryMemoryStore"/> argument constraints, capability claims, and observability.</summary>
public sealed class InMemoryMemoryStoreTests
{
    private static readonly MemoryStoreKey _key = new("memory");

    [Fact]
    public void Constructor_WhenADependencyIsInvalid_ThrowsNamingIt()
    {
        var grants = new TestGoalGrants();
        var ids = new TestIntentIds();

        Should.Throw<ArgumentNullException>(() => new InMemoryMemoryStore(default, grants, ids, TimeProvider.System)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => new InMemoryMemoryStore(_key, null!, ids, TimeProvider.System)).ParamName.ShouldBe("grants");
        Should.Throw<ArgumentNullException>(() => new InMemoryMemoryStore(_key, grants, null!, TimeProvider.System)).ParamName.ShouldBe("intentIds");
        Should.Throw<ArgumentNullException>(() => new InMemoryMemoryStore(_key, grants, ids, null!)).ParamName.ShouldBe("time");
    }

    [Fact]
    public void Descriptor_WhenConstructed_ClaimsNoDurabilityAndNamesItsKeyAndAudience()
    {
        var store = NewStore(new TestGoalGrants());

        store.Descriptor.Key.ShouldBe(_key);
        store.Descriptor.IsDurable.ShouldBeFalse();
        store.Descriptor.SecurityAudience.ShouldBe(new ComponentId("agentkit.memory.in-memory"));
    }

    [Fact]
    public async Task Operations_WhenRequestIsNull_ThrowArgumentNullExceptionBeforeAnyEffect()
    {
        var store = NewStore(new TestGoalGrants());

        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.WriteAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.ReadAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.ListAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.TransitionAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.DeleteAsync(null!))).ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task WriteAsync_WhenObserved_EmitsOneSpanWithTenantAndABoundedMetric()
    {
        var grants = new TestGoalGrants();
        var store = NewStore(grants);
        var owner = MemoryTestData.NewOwner($"tenant-{Guid.NewGuid():N}");
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.MemoryStoreOperation
                && observation.GetTagItem(AgentKitTagNames.TenantId)?.ToString() == owner.Identity.TenantId.Value
                && observation.GetTagItem(AgentKitTagNames.MemoryStoreFamily)?.ToString() == "memory");
        using var metrics = new MetricCollector(AgentKitMetricNames.MemoryStoreOperationCount);
        var request = new MemoryStoreRequestFactory(grants, store.Descriptor.SecurityAudience).Write(MemoryTestData.Record(owner), owner.Authorization);

        _ = await store.WriteAsync(request, TestContext.Current.CancellationToken);

        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Ok);
        span.GetTagItem(AgentKitTagNames.MemoryStoreOperation).ShouldBe("write");
        span.GetTagItem(AgentKitTagNames.MemoryStoreAdapter).ShouldBe("in_memory");
        span.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("completed");
        var observed = metrics.Snapshot();
        observed.ShouldContain(static measurement =>
            measurement.Tags[AgentKitTagNames.MemoryStoreOperation]!.Equals("write") && measurement.Tags[AgentKitTagNames.Outcome]!.Equals("completed"));
        observed.ShouldAllBe(static measurement => measurement.Tags.Keys.Order().SequenceEqual(
            new[] { AgentKitTagNames.MemoryStoreAdapter, AgentKitTagNames.MemoryStoreFamily, AgentKitTagNames.MemoryStoreOperation, AgentKitTagNames.Outcome }.Order()));
    }

    [Fact]
    public async Task WriteAsync_WhenDenied_MarksTheSpanFailedWithTheBoundedOutcomeAndLogsContentFree()
    {
        var grants = new TestGoalGrants { ForcedStatus = GrantConsumptionStatus.Revoked };
        var logger = new RecordingLogger<InMemoryMemoryStore>();
        var store = NewStore(grants, logger);
        var owner = MemoryTestData.NewOwner($"tenant-{Guid.NewGuid():N}");
        var record = MemoryTestData.Record(owner, "a confidential preference");
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.MemoryStoreOperation
                && observation.GetTagItem(AgentKitTagNames.TenantId)?.ToString() == owner.Identity.TenantId.Value);
        var request = new MemoryStoreRequestFactory(new TestGoalGrants(), store.Descriptor.SecurityAudience).Write(record, owner.Authorization);

        _ = await store.WriteAsync(request, TestContext.Current.CancellationToken);

        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("denied");
        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(32000);
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldNotContain("confidential");
    }

    [Fact]
    public async Task WriteAsync_WhenTheLoggerThrows_StillReturnsTheSemanticResult()
    {
        var grants = new TestGoalGrants();
        var store = NewStore(grants, new RecordingLogger<InMemoryMemoryStore> { ThrowOnWrite = true });
        var owner = MemoryTestData.NewOwner($"tenant-{Guid.NewGuid():N}");
        var record = MemoryTestData.Record(owner);

        var result = await store.WriteAsync(new MemoryStoreRequestFactory(grants, store.Descriptor.SecurityAudience).Write(record, owner.Authorization), TestContext.Current.CancellationToken);

        result.Record.ShouldBe(record);
    }

    [Fact]
    public async Task WriteAsync_WhenCancelled_RecordsACancelledOutcomeAndRethrows()
    {
        var grants = new TestGoalGrants();
        var store = NewStore(grants);
        var owner = MemoryTestData.NewOwner($"tenant-{Guid.NewGuid():N}");
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.MemoryStoreOperation
                && observation.GetTagItem(AgentKitTagNames.TenantId)?.ToString() == owner.Identity.TenantId.Value);
        var request = new MemoryStoreRequestFactory(grants, store.Descriptor.SecurityAudience).Write(MemoryTestData.Record(owner), owner.Authorization);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await store.WriteAsync(request, cancelled.Token));

        activities.Snapshot().ShouldHaveSingleItem().GetTagItem(AgentKitTagNames.Outcome).ShouldBe("cancelled");
    }

    [Fact]
    public async Task ReadAsync_WhenTheRecordBodyIsProtected_NeverAppearsInSpanTagsOrLogs()
    {
        var grants = new TestGoalGrants();
        var logger = new RecordingLogger<InMemoryMemoryStore>();
        var store = NewStore(grants, logger);
        var owner = MemoryTestData.NewOwner($"tenant-{Guid.NewGuid():N}");
        var record = MemoryTestData.Record(owner, "protected body text");
        var factory = new MemoryStoreRequestFactory(grants, store.Descriptor.SecurityAudience);
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.MemoryStoreOperation
                && observation.GetTagItem(AgentKitTagNames.TenantId)?.ToString() == owner.Identity.TenantId.Value);
        _ = await store.WriteAsync(factory.Write(record, owner.Authorization), TestContext.Current.CancellationToken);

        _ = await store.ReadAsync(factory.Read(record.Id, owner.Authorization), TestContext.Current.CancellationToken);

        activities.Snapshot().SelectMany(static span => span.Tags.Select(static tag => tag.Value)).Any(static value => value?.ToString()?.Contains("protected body", StringComparison.Ordinal) == true).ShouldBeFalse();
        logger.Snapshot().ShouldAllBe(static entry => !entry.Message.Contains("protected body", StringComparison.Ordinal));
    }

    private static InMemoryMemoryStore NewStore(TestGoalGrants grants, ILogger<InMemoryMemoryStore>? logger = null) =>
        new(_key, grants, new TestIntentIds(), TimeProvider.System, logger);
}
