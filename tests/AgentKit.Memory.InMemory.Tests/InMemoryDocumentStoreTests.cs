// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.InMemory.Tests;

using System.Diagnostics;

using AgentKit.Observability;

/// <summary>Verifies <see cref="InMemoryDocumentStore"/> argument constraints, capability claims, and observability.</summary>
public sealed class InMemoryDocumentStoreTests
{
    private static readonly DocumentStoreKey _key = new("documents");

    [Fact]
    public void Constructor_WhenADependencyIsInvalid_ThrowsNamingIt()
    {
        var grants = new TestGoalGrants();
        var ids = new TestIntentIds();

        Should.Throw<ArgumentNullException>(() => new InMemoryDocumentStore(default, grants, ids, TimeProvider.System)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => new InMemoryDocumentStore(_key, null!, ids, TimeProvider.System)).ParamName.ShouldBe("grants");
        Should.Throw<ArgumentNullException>(() => new InMemoryDocumentStore(_key, grants, null!, TimeProvider.System)).ParamName.ShouldBe("intentIds");
        Should.Throw<ArgumentNullException>(() => new InMemoryDocumentStore(_key, grants, ids, null!)).ParamName.ShouldBe("time");
    }

    [Fact]
    public void Descriptor_WhenConstructed_ClaimsNoDurabilityAndNamesItsKeyAndAudience()
    {
        var store = new InMemoryDocumentStore(_key, new TestGoalGrants(), new TestIntentIds(), TimeProvider.System);

        store.Descriptor.Key.ShouldBe(_key);
        store.Descriptor.IsDurable.ShouldBeFalse();
        store.Descriptor.SecurityAudience.ShouldBe(new ComponentId("agentkit.documents.in-memory"));
    }

    [Fact]
    public async Task Operations_WhenRequestIsNull_ThrowArgumentNullExceptionBeforeAnyEffect()
    {
        var store = new InMemoryDocumentStore(_key, new TestGoalGrants(), new TestIntentIds(), TimeProvider.System);

        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.WriteAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.ActivateAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.ReadAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.DeleteAsync(null!))).ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task WriteAsync_WhenObserved_EmitsOneSpanWithTheDocumentFamilyAndABoundedMetric()
    {
        var grants = new TestGoalGrants();
        var store = new InMemoryDocumentStore(_key, grants, new TestIntentIds(), TimeProvider.System);
        var owner = MemoryTestData.NewOwner($"tenant-{Guid.NewGuid():N}");
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.MemoryStoreOperation
                && observation.GetTagItem(AgentKitTagNames.TenantId)?.ToString() == owner.Identity.TenantId.Value
                && observation.GetTagItem(AgentKitTagNames.MemoryStoreFamily)?.ToString() == "document");
        using var metrics = new MetricCollector(AgentKitMetricNames.MemoryStoreOperationCount);
        var record = MemoryTestData.Document(owner);
        var request = new DocumentStoreRequestFactory(grants, store.Descriptor.SecurityAudience).Write(record, MemoryTestData.Chunks(record), true, owner.Authorization);

        _ = await store.WriteAsync(request, TestContext.Current.CancellationToken);

        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Ok);
        span.GetTagItem(AgentKitTagNames.MemoryStoreOperation).ShouldBe("write");
        metrics.Snapshot().ShouldContain(static measurement =>
            measurement.Tags[AgentKitTagNames.MemoryStoreFamily]!.Equals("document") && measurement.Tags[AgentKitTagNames.Outcome]!.Equals("completed"));
    }

    [Fact]
    public async Task ActivateAsync_WhenDenied_ReportsTheDeniedOutcomeAndLogsAWarning()
    {
        var grants = new TestGoalGrants { ForcedStatus = GrantConsumptionStatus.Revoked };
        var logger = new RecordingLogger<InMemoryDocumentStore>();
        var store = new InMemoryDocumentStore(_key, grants, new TestIntentIds(), TimeProvider.System, logger);
        var owner = MemoryTestData.NewOwner($"tenant-{Guid.NewGuid():N}");
        var request = new DocumentStoreRequestFactory(new TestGoalGrants(), store.Descriptor.SecurityAudience).Activate(new DocumentId(Guid.NewGuid()), "v1", null, owner.Authorization);

        var result = await store.ActivateAsync(request, TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Denied);
        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(32000);
        entry.Level.ShouldBe(LogLevel.Warning);
    }

    [Fact]
    public async Task WriteAsync_WhenTheLoggerThrows_StillReturnsTheSemanticResult()
    {
        var grants = new TestGoalGrants();
        var store = new InMemoryDocumentStore(_key, grants, new TestIntentIds(), TimeProvider.System, new RecordingLogger<InMemoryDocumentStore> { ThrowOnWrite = true });
        var owner = MemoryTestData.NewOwner($"tenant-{Guid.NewGuid():N}");
        var record = MemoryTestData.Document(owner);

        var result = await store.WriteAsync(new DocumentStoreRequestFactory(grants, store.Descriptor.SecurityAudience).Write(record, MemoryTestData.Chunks(record), true, owner.Authorization), TestContext.Current.CancellationToken);

        result.IsWritten.ShouldBeTrue();
    }
}
