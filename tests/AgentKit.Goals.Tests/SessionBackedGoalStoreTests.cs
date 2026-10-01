// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

/// <summary>Verifies session-backed projection construction, constraints, replay reconstruction, and honest claims.</summary>
public sealed class SessionBackedGoalStoreTests
{
    [Fact]
    public void Constructor_WhenADependencyIsNull_ThrowsArgumentNullExceptionNamingIt()
    {
        using var fixture = new SessionBackedGoalStoreConformanceFixture();
        var profile = TestSecurityEvidence.SessionProfile();
        var grants = new TestGoalGrants();
        var ids = new FixedIds();
        var entryIds = new FixedEntryIds();
        var options = new SessionBackedGoalStoreOptions();
        var coordinator = new UnsupportedSessionCoordinator();

        Should.Throw<ArgumentNullException>(() => new SessionBackedGoalStore(null!, profile, grants, ids, entryIds, TimeProvider.System, options)).ParamName.ShouldBe("sessions");
        Should.Throw<ArgumentNullException>(() => new SessionBackedGoalStore(coordinator, null!, grants, ids, entryIds, TimeProvider.System, options)).ParamName.ShouldBe("profile");
        Should.Throw<ArgumentNullException>(() => new SessionBackedGoalStore(coordinator, profile, null!, ids, entryIds, TimeProvider.System, options)).ParamName.ShouldBe("grants");
        Should.Throw<ArgumentNullException>(() => new SessionBackedGoalStore(coordinator, profile, grants, null!, entryIds, TimeProvider.System, options)).ParamName.ShouldBe("intentIds");
        Should.Throw<ArgumentNullException>(() => new SessionBackedGoalStore(coordinator, profile, grants, ids, null!, TimeProvider.System, options)).ParamName.ShouldBe("entryIds");
        Should.Throw<ArgumentNullException>(() => new SessionBackedGoalStore(coordinator, profile, grants, ids, entryIds, null!, options)).ParamName.ShouldBe("time");
        Should.Throw<ArgumentNullException>(() => new SessionBackedGoalStore(coordinator, profile, grants, ids, entryIds, TimeProvider.System, null!)).ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenMaximumAppendAttemptsIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        var options = new SessionBackedGoalStoreOptions { MaximumAppendAttempts = 0 };

        Should.Throw<ArgumentOutOfRangeException>(() => new SessionBackedGoalStore(
            new UnsupportedSessionCoordinator(), TestSecurityEvidence.SessionProfile(), new TestGoalGrants(), new FixedIds(), new FixedEntryIds(), TimeProvider.System, options))
            .ParamName.ShouldBe("options");
    }

    [Fact]
    public void Descriptor_WhenRead_ClaimsNeitherDurabilityNorIntentDiscovery()
    {
        using var fixture = new SessionBackedGoalStoreConformanceFixture();

        fixture.Store.Descriptor.IsDurable.ShouldBeFalse();
        fixture.Store.Descriptor.SupportsIntentDiscovery.ShouldBeFalse();
    }

    [Fact]
    public async Task Operations_WhenRequestIsNull_ThrowArgumentNullException()
    {
        using var fixture = new SessionBackedGoalStoreConformanceFixture();

        (await Should.ThrowAsync<ArgumentNullException>(async () => await fixture.Store.CreateAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await fixture.Store.LoadAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await fixture.Store.TransitionAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await fixture.Store.ReadChildrenAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await fixture.Store.ReadIntentsAsync(null!))).ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task LoadAsync_WhenAFreshStoreReadsTheSameSession_ReconstructsStatusOwnershipAndAttemptsFromEntries()
    {
        using var fixture = new SessionBackedGoalStoreConformanceFixture();
        var owner = await fixture.CreateOwnerAsync("tenant", TestContext.Current.CancellationToken);
        var factory = new GoalRequestFactory(fixture.Grants, fixture.Store.Descriptor.SecurityAudience);
        var created = (await fixture.Store.CreateAsync(
            factory.Create(GoalTestData.Goal(owner.AgentId, owner.SessionId, owner.RunId), owner.Authorization, "create"), TestContext.Current.CancellationToken))
            .ShouldBeOfType<GoalCreated>().Record;
        var ready = (await fixture.Store.TransitionAsync(
            factory.Transition(GoalTestData.Transition(created, GoalStatus.Ready, "ready"), null, owner.Authorization), TestContext.Current.CancellationToken))
            .ShouldBeOfType<GoalTransitioned>().Record;
        var attempt = GoalTestData.Attempt(created.Goal.Id, 1, GoalTestData.NewAgent(), GoalTestData.NewSession(), GoalTestData.NewRun());
        var active = (await fixture.Store.TransitionAsync(
            factory.Transition(GoalTestData.Transition(ready, GoalStatus.Active, "active"), new GoalAttemptStart(attempt), owner.Authorization), TestContext.Current.CancellationToken))
            .ShouldBeOfType<GoalTransitioned>().Record;
        var fresh = await fixture.ReopenAsync(TestContext.Current.CancellationToken);

        var loaded = (await fresh.LoadAsync(
            new GoalRequestFactory(fixture.Grants, fresh.Descriptor.SecurityAudience).Load(created.Goal.Id, owner.Authorization), TestContext.Current.CancellationToken))
            .ShouldBeOfType<GoalLoaded>().Record;

        loaded.ShouldBe(active);
        loaded.Goal.OwnerAgentId.ShouldBe(owner.AgentId);
        loaded.Transitions.Select(static transition => transition.To).ShouldBe([GoalStatus.Ready, GoalStatus.Active]);
    }

    [Fact]
    public async Task CreateAsync_WhenADelegationIsSupplied_ReturnsItOnCreationButDoesNotPersistItInSessionHistory()
    {
        using var fixture = new SessionBackedGoalStoreConformanceFixture();
        var owner = await fixture.CreateOwnerAsync("tenant", TestContext.Current.CancellationToken);
        var factory = new GoalRequestFactory(fixture.Grants, fixture.Store.Descriptor.SecurityAudience);
        var parent = await ActivateRootAsync(fixture, factory, owner);
        var child = GoalTestData.Goal(owner.AgentId, owner.SessionId, owner.RunId, parent.Goal.Id);
        var delegation = GoalTestData.Delegation(parent.Goal, parent.ActiveAttempt!.Id, owner.RunId, GoalTestData.NewAgent(), "d", owner.Authorization);

        var created = (await fixture.Store.CreateAsync(factory.Create(child, owner.Authorization, "child", delegation), TestContext.Current.CancellationToken))
            .ShouldBeOfType<GoalCreated>();
        var loaded = (await fixture.Store.LoadAsync(factory.Load(child.Id, owner.Authorization), TestContext.Current.CancellationToken))
            .ShouldBeOfType<GoalLoaded>().Record;

        created.Record.Delegation.ShouldBe(delegation);
        loaded.Delegation.ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_WhenTheOwningSessionDoesNotExist_RejectsWithNotFound()
    {
        using var fixture = new SessionBackedGoalStoreConformanceFixture();
        var agent = GoalTestData.NewAgent();
        var session = GoalTestData.NewSession();
        var run = GoalTestData.NewRun();
        var authorization = GoalTestData.Authorization(agent, session, run);
        var factory = new GoalRequestFactory(fixture.Grants, fixture.Store.Descriptor.SecurityAudience);

        var result = await fixture.Store.CreateAsync(
            factory.Create(GoalTestData.Goal(agent, session, run), authorization, "create"), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<GoalCreateRejected>().Failure.Kind.ShouldBe(GoalStoreFailureKind.NotFound);
    }

    [Fact]
    public async Task ReadIntentsAsync_WhenCalled_AlwaysReportsUnavailable()
    {
        using var fixture = new SessionBackedGoalStoreConformanceFixture();

        var result = await fixture.Store.ReadIntentsAsync(new GoalIntentScanRequest(new ComponentId("w"), 0, 1), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<GoalPageRejected>().Failure.Kind.ShouldBe(GoalStoreFailureKind.Unavailable);
    }

    private static async Task<GoalRecord> ActivateRootAsync(
        SessionBackedGoalStoreConformanceFixture fixture, GoalRequestFactory factory, GoalConformanceOwner owner)
    {
        var created = (await fixture.Store.CreateAsync(
            factory.Create(GoalTestData.Goal(owner.AgentId, owner.SessionId, owner.RunId), owner.Authorization, "root"), TestContext.Current.CancellationToken))
            .ShouldBeOfType<GoalCreated>().Record;
        var ready = (await fixture.Store.TransitionAsync(
            factory.Transition(GoalTestData.Transition(created, GoalStatus.Ready, "ready"), null, owner.Authorization), TestContext.Current.CancellationToken))
            .ShouldBeOfType<GoalTransitioned>().Record;
        var attempt = GoalTestData.Attempt(created.Goal.Id, 1, GoalTestData.NewAgent(), GoalTestData.NewSession(), GoalTestData.NewRun());
        return (await fixture.Store.TransitionAsync(
            factory.Transition(GoalTestData.Transition(ready, GoalStatus.Active, "active"), new GoalAttemptStart(attempt), owner.Authorization), TestContext.Current.CancellationToken))
            .ShouldBeOfType<GoalTransitioned>().Record;
    }

    private sealed class FixedIds: IIdentifierGenerator<SecurityEnforcementIntentId>
    {
        public SecurityEnforcementIntentId Create() => new(Guid.NewGuid());
    }

    private sealed class FixedEntryIds: IIdentifierGenerator<SessionEntryId>
    {
        public SessionEntryId Create() => new(Guid.NewGuid());
    }
}
