// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.InMemory.Tests;

using System.Diagnostics;

using AgentKit.Observability;

/// <summary>Verifies in-memory store construction, argument constraints, claims, and observability.</summary>
public sealed class InMemoryGoalStoreTests
{
    [Fact]
    public void Constructor_WhenADependencyIsNull_ThrowsArgumentNullExceptionNamingIt()
    {
        var grants = new TestGoalGrants();
        var ids = new DelegateIds();
        var options = new InMemoryGoalStoreOptions();

        Should.Throw<ArgumentNullException>(() => new InMemoryGoalStore(null!, ids, TimeProvider.System, options)).ParamName.ShouldBe("grants");
        Should.Throw<ArgumentNullException>(() => new InMemoryGoalStore(grants, null!, TimeProvider.System, options)).ParamName.ShouldBe("intentIds");
        Should.Throw<ArgumentNullException>(() => new InMemoryGoalStore(grants, ids, null!, options)).ParamName.ShouldBe("time");
        Should.Throw<ArgumentNullException>(() => new InMemoryGoalStore(grants, ids, TimeProvider.System, null!)).ParamName.ShouldBe("options");
    }

    [Fact]
    public void Descriptor_WhenConstructed_ClaimsNeitherDurabilityNorMoreThanOneProcess()
    {
        var store = NewStore(new TestGoalGrants());

        store.Descriptor.IsDurable.ShouldBeFalse();
        store.Descriptor.SupportsIntentDiscovery.ShouldBeTrue();
        store.Descriptor.SecurityAudience.ShouldBe(new ComponentId("agentkit.goals.in-memory"));
    }

    [Fact]
    public async Task Operations_WhenRequestIsNull_ThrowArgumentNullExceptionBeforeAnyEffect()
    {
        var store = NewStore(new TestGoalGrants());

        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.CreateAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.LoadAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.TransitionAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.ReadChildrenAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.ReadIntentsAsync(null!))).ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task CreateAsync_WhenObserved_EmitsOneSpanWithIdentitiesAndABoundedMetric()
    {
        var grants = new TestGoalGrants();
        var store = NewStore(grants);
        var owner = Owner();
        var goal = GoalTestData.Goal(owner.Agent, owner.Session, owner.Run);
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.GoalStoreOperation
                && observation.GetTagItem(AgentKitTagNames.GoalId)?.ToString() == goal.Id.ToString());
        using var metrics = new MetricCollector(AgentKitMetricNames.GoalStoreOperationCount);
        var request = new GoalRequestFactory(grants, store.Descriptor.SecurityAudience).Create(goal, owner.Authorization, "create-1");

        _ = await store.CreateAsync(request, TestContext.Current.CancellationToken);

        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Ok);
        span.GetTagItem(AgentKitTagNames.GoalStoreOperation).ShouldBe("create");
        span.GetTagItem(AgentKitTagNames.GoalStoreAdapter).ShouldBe("in_memory");
        span.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("completed");
        span.GetTagItem(AgentKitTagNames.TenantId).ShouldBe("tenant");
        var observed = metrics.Snapshot();
        observed.ShouldContain(static measurement =>
            measurement.Tags[AgentKitTagNames.GoalStoreOperation]!.Equals("create")
            && measurement.Tags[AgentKitTagNames.Outcome]!.Equals("completed"));
        observed.ShouldAllBe(static measurement => measurement.Tags.Keys.Order().SequenceEqual(
            new[] { AgentKitTagNames.GoalStoreAdapter, AgentKitTagNames.GoalStoreOperation, AgentKitTagNames.Outcome }.Order()));
    }

    [Fact]
    public async Task CreateAsync_WhenDenied_MarksTheSpanFailedWithTheBoundedOutcomeAndLogsAWarning()
    {
        var grants = new TestGoalGrants { ForcedStatus = GrantConsumptionStatus.Revoked };
        var logger = new RecordingLogger<InMemoryGoalStore>();
        var store = NewStore(grants, logger);
        var owner = Owner();
        var goal = GoalTestData.Goal(owner.Agent, owner.Session, owner.Run);
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.GoalStoreOperation
                && observation.GetTagItem(AgentKitTagNames.GoalId)?.ToString() == goal.Id.ToString());
        var request = new GoalRequestFactory(new TestGoalGrants(), store.Descriptor.SecurityAudience).Create(goal, owner.Authorization, "create-1");

        _ = await store.CreateAsync(request, TestContext.Current.CancellationToken);

        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("denied");
        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(31100);
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldNotContain(goal.Definition.Objective);
    }

    [Fact]
    public async Task CreateAsync_WhenTheLoggerThrows_StillReturnsTheSemanticResult()
    {
        var grants = new TestGoalGrants();
        var store = NewStore(grants, new RecordingLogger<InMemoryGoalStore> { ThrowOnWrite = true });
        var owner = Owner();
        var goal = GoalTestData.Goal(owner.Agent, owner.Session, owner.Run);
        var request = new GoalRequestFactory(grants, store.Descriptor.SecurityAudience).Create(goal, owner.Authorization, "create-1");

        var result = await store.CreateAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<GoalCreated>().Record.Goal.Id.ShouldBe(goal.Id);
    }

    [Fact]
    public async Task CreateAsync_WhenCancelled_RecordsACancelledOutcomeAndRethrows()
    {
        var grants = new TestGoalGrants();
        var store = NewStore(grants);
        var owner = Owner();
        var goal = GoalTestData.Goal(owner.Agent, owner.Session, owner.Run);
        var request = new GoalRequestFactory(grants, store.Descriptor.SecurityAudience).Create(goal, owner.Authorization, "create-1");
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.GoalStoreOperation
                && observation.GetTagItem(AgentKitTagNames.GoalId)?.ToString() == goal.Id.ToString());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await store.CreateAsync(request, cancelled.Token));

        activities.Snapshot().ShouldHaveSingleItem().GetTagItem(AgentKitTagNames.Outcome).ShouldBe("cancelled");
    }

    private static InMemoryGoalStore NewStore(TestGoalGrants grants, ILogger<InMemoryGoalStore>? logger = null) =>
        new(grants, new DelegateIds(), TimeProvider.System, new InMemoryGoalStoreOptions(), logger);

    private static (AgentId Agent, SessionId Session, RunId Run, SecurityAuthorizationContext Authorization) Owner()
    {
        var agent = GoalTestData.NewAgent();
        var session = GoalTestData.NewSession();
        var run = GoalTestData.NewRun();
        return (agent, session, run, GoalTestData.Authorization(agent, session, run));
    }

    private sealed class DelegateIds: IIdentifierGenerator<SecurityEnforcementIntentId>
    {
        public SecurityEnforcementIntentId Create() => new(Guid.NewGuid());
    }
}
