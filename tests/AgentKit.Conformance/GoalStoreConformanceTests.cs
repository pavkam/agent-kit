// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.TestSupport;

/// <summary>Defines portable creation, transition, attempt, children-paging, authorization, and replay behavior for <see cref="IGoalStore"/>.</summary>
/// <typeparam name="TFixture">The adapter-specific isolated fixture.</typeparam>
/// <remarks>
/// Every case is required contract behavior for all goal-store adapters. Durability cases run when the fixture declares
/// <see cref="ConformanceCapabilities.SupportsDurability"/>, delegation round-trip cases when it declares
/// <see cref="IGoalStoreConformanceFixture.PreservesDelegation"/>, and intent-discovery cases when it offers a scanner;
/// an adapter that lacks one skips the case rather than reporting a guarantee it does not provide.
/// </remarks>
public abstract class GoalStoreConformanceTests<TFixture>
    where TFixture : IGoalStoreConformanceFixture, new()
{
    /// <summary>Verifies a new goal persists as proposed at the initial version.</summary>
    [Fact]
    public async Task CreateAsync_WhenGoalIsNew_PersistsProposedGoalAtTheInitialVersion()
    {
        var fixture = new TFixture();
        var owner = await fixture.CreateOwnerAsync("tenant", TestContext.Current.CancellationToken);
        var goal = GoalTestData.Goal(owner.AgentId, owner.SessionId, owner.RunId);

        var created = await CreateAsync(fixture, owner, goal, "create-1");

        created.Replayed.ShouldBeFalse();
        created.Record.Goal.Status.ShouldBe(GoalStatus.Proposed);
        created.Record.Goal.Version.ShouldBe(new VersionToken("1"));
        created.Record.Goal.Id.ShouldBe(goal.Id);
        created.Record.Sequence.ShouldBeGreaterThan(0);
        created.Record.ChildOrdinal.ShouldBeNull();
    }

    /// <summary>Verifies an equivalent creation replay returns the original goal and creates no duplicate.</summary>
    [Fact]
    public async Task CreateAsync_WhenReplayedWithTheSameKey_ReturnsTheOriginalGoalWithoutDuplicating()
    {
        var fixture = new TFixture();
        var owner = await fixture.CreateOwnerAsync("tenant", TestContext.Current.CancellationToken);
        var goal = GoalTestData.Goal(owner.AgentId, owner.SessionId, owner.RunId);
        var first = await CreateAsync(fixture, owner, goal, "create-1");

        var replay = await CreateAsync(fixture, owner, goal, "create-1");

        replay.Replayed.ShouldBeTrue();
        replay.Record.ShouldBe(first.Record);
    }

    /// <summary>Verifies reusing a creation key for a different goal is refused.</summary>
    [Fact]
    public async Task CreateAsync_WhenKeyIsReusedForADifferentGoal_RejectsWithIdempotencyConflict()
    {
        var fixture = new TFixture();
        var owner = await fixture.CreateOwnerAsync("tenant", TestContext.Current.CancellationToken);
        _ = await CreateAsync(fixture, owner, GoalTestData.Goal(owner.AgentId, owner.SessionId, owner.RunId), "create-1");

        var rejected = await TryCreateAsync(fixture, owner, GoalTestData.Goal(owner.AgentId, owner.SessionId, owner.RunId), "create-1");

        rejected.Failure.Kind.ShouldBe(GoalStoreFailureKind.IdempotencyConflict);
    }

    /// <summary>Verifies a goal identity cannot be reused under a different creation key.</summary>
    [Fact]
    public async Task CreateAsync_WhenGoalIdentityIsAlreadyInUse_RejectsWithIdempotencyConflict()
    {
        var fixture = new TFixture();
        var owner = await fixture.CreateOwnerAsync("tenant", TestContext.Current.CancellationToken);
        var goal = GoalTestData.Goal(owner.AgentId, owner.SessionId, owner.RunId);
        _ = await CreateAsync(fixture, owner, goal, "create-1");

        var rejected = await TryCreateAsync(fixture, owner, goal, "create-2");

        rejected.Failure.Kind.ShouldBe(GoalStoreFailureKind.IdempotencyConflict);
    }

    /// <summary>Verifies a grant bound to a different goal is denied before anything is written.</summary>
    [Fact]
    public async Task CreateAsync_WhenGrantBindsADifferentGoal_DeniesBeforeAnyWrite()
    {
        var fixture = new TFixture();
        var owner = await fixture.CreateOwnerAsync("tenant", TestContext.Current.CancellationToken);
        var goal = GoalTestData.Goal(owner.AgentId, owner.SessionId, owner.RunId);
        var other = GoalTestData.Goal(owner.AgentId, owner.SessionId, owner.RunId);
        var forged = Factory(fixture).Create(other, owner.Authorization, "create-1");
        var request = new GoalCreateRequest(goal, null, new IdempotencyKey("create-1"), forged.Grant);

        var result = await fixture.Store.CreateAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<GoalCreateRejected>().Failure.Kind.ShouldBe(GoalStoreFailureKind.Denied);
        (await TryLoadAsync(fixture, owner, goal.Id)).Failure.Kind.ShouldBe(GoalStoreFailureKind.NotFound);
    }

    /// <summary>Verifies a consumed single-use grant cannot authorize a second write.</summary>
    [Fact]
    public async Task CreateAsync_WhenGrantWasAlreadyConsumed_Denies()
    {
        var fixture = new TFixture();
        var owner = await fixture.CreateOwnerAsync("tenant", TestContext.Current.CancellationToken);
        var request = Factory(fixture).Create(GoalTestData.Goal(owner.AgentId, owner.SessionId, owner.RunId), owner.Authorization, "create-1");
        _ = await fixture.Store.CreateAsync(request, TestContext.Current.CancellationToken);

        var second = await fixture.Store.CreateAsync(request, TestContext.Current.CancellationToken);

        second.ShouldBeOfType<GoalCreateRejected>().Failure.Kind.ShouldBe(GoalStoreFailureKind.Denied);
    }

    /// <summary>Verifies an unavailable grant store fails the operation closed.</summary>
    [Fact]
    public async Task CreateAsync_WhenGrantStoreIsUnavailable_DeniesAndWritesNothing()
    {
        var fixture = new TFixture();
        var owner = await fixture.CreateOwnerAsync("tenant", TestContext.Current.CancellationToken);
        var goal = GoalTestData.Goal(owner.AgentId, owner.SessionId, owner.RunId);
        var request = Factory(fixture).Create(goal, owner.Authorization, "create-1");
        fixture.Grants.Fail = true;

        var result = await fixture.Store.CreateAsync(request, TestContext.Current.CancellationToken);
        fixture.Grants.Fail = false;

        result.ShouldBeOfType<GoalCreateRejected>().Failure.Kind.ShouldBe(GoalStoreFailureKind.Denied);
        (await TryLoadAsync(fixture, owner, goal.Id)).Failure.Kind.ShouldBe(GoalStoreFailureKind.NotFound);
    }

    /// <summary>Verifies a grant scoped to another agent cannot create a goal owned by this agent.</summary>
    [Fact]
    public async Task CreateAsync_WhenAuthorizedScopeOwnsADifferentAgent_RejectsWithScopeMismatch()
    {
        var fixture = new TFixture();
        var owner = await fixture.CreateOwnerAsync("tenant", TestContext.Current.CancellationToken);
        var stranger = await fixture.CreateOwnerAsync("tenant", TestContext.Current.CancellationToken);
        var goal = GoalTestData.Goal(owner.AgentId, owner.SessionId, owner.RunId);

        var rejected = await TryCreateAsync(fixture, stranger, goal, "create-1");

        rejected.Failure.Kind.ShouldBe(GoalStoreFailureKind.ScopeMismatch);
    }

    /// <summary>Verifies another tenant cannot observe a goal and sees it as absent.</summary>
    [Fact]
    public async Task LoadAsync_WhenGoalBelongsToAnotherTenant_ReportsNotFound()
    {
        var fixture = new TFixture();
        var owner = await fixture.CreateOwnerAsync("tenant-a", TestContext.Current.CancellationToken);
        var foreign = await fixture.CreateOwnerAsync("tenant-b", TestContext.Current.CancellationToken);
        var goal = GoalTestData.Goal(owner.AgentId, owner.SessionId, owner.RunId);
        _ = await CreateAsync(fixture, owner, goal, "create-1");

        var rejected = await TryLoadAsync(fixture, foreign, goal.Id);

        rejected.Failure.Kind.ShouldBe(GoalStoreFailureKind.NotFound);
    }

    /// <summary>Verifies a same-tenant caller scoped to another agent or session cannot read the goal, without learning whether it exists.</summary>
    [Fact]
    public async Task LoadAsync_WhenCallerScopeOwnsAnotherAgent_RejectsWithScopeMismatch()
    {
        var fixture = new TFixture();
        var owner = await fixture.CreateOwnerAsync("tenant", TestContext.Current.CancellationToken);
        var stranger = await fixture.CreateOwnerAsync("tenant", TestContext.Current.CancellationToken);
        var goal = GoalTestData.Goal(owner.AgentId, owner.SessionId, owner.RunId);
        _ = await CreateAsync(fixture, owner, goal, "create-1");

        var rejected = await TryLoadAsync(fixture, stranger, goal.Id);

        rejected.Failure.Kind.ShouldBeOneOf(GoalStoreFailureKind.ScopeMismatch, GoalStoreFailureKind.NotFound);
    }

    /// <summary>Verifies an applied transition moves status and increments the version.</summary>
    [Fact]
    public async Task TransitionAsync_WhenExpectedVersionMatches_AppliesAndIncrementsTheVersion()
    {
        var fixture = new TFixture();
        var (owner, created) = await CreateGoalAsync(fixture);

        var applied = await TransitionAsync(fixture, owner, created, GoalStatus.Ready, "ready-1");

        applied.Replayed.ShouldBeFalse();
        applied.Record.Goal.Status.ShouldBe(GoalStatus.Ready);
        applied.Record.Goal.Version.ShouldBe(new VersionToken("2"));
        applied.Record.Transitions.Length.ShouldBe(1);
    }

    /// <summary>Verifies a stale expected version is refused and leaves the goal untouched.</summary>
    [Fact]
    public async Task TransitionAsync_WhenExpectedVersionIsStale_RejectsWithVersionConflict()
    {
        var fixture = new TFixture();
        var (owner, created) = await CreateGoalAsync(fixture);
        _ = await TransitionAsync(fixture, owner, created, GoalStatus.Ready, "ready-1");

        var stale = await TryTransitionAsync(fixture, owner, created, GoalStatus.Cancelled, "cancel-1");

        stale.Failure.Kind.ShouldBe(GoalStoreFailureKind.VersionConflict);
        (await LoadAsync(fixture, owner, created.Goal.Id)).Goal.Status.ShouldBe(GoalStatus.Ready);
    }

    /// <summary>Verifies replaying a transition returns the recorded state without a second change.</summary>
    [Fact]
    public async Task TransitionAsync_WhenReplayedWithTheSameKey_ReturnsTheRecordedStateWithoutASecondTransition()
    {
        var fixture = new TFixture();
        var (owner, created) = await CreateGoalAsync(fixture);
        var first = await TransitionAsync(fixture, owner, created, GoalStatus.Ready, "ready-1");
        var transition = first.Record.Transitions[0];

        var replay = (await fixture.Store.TransitionAsync(
            Factory(fixture).Transition(transition, null, owner.Authorization), TestContext.Current.CancellationToken))
            .ShouldBeOfType<GoalTransitioned>();

        replay.Replayed.ShouldBeTrue();
        replay.Record.Transitions.Length.ShouldBe(1);
        replay.Record.Goal.Version.ShouldBe(first.Record.Goal.Version);
    }

    /// <summary>Verifies reusing a transition key for a different change is refused.</summary>
    [Fact]
    public async Task TransitionAsync_WhenKeyIsReusedForADifferentTransition_RejectsWithIdempotencyConflict()
    {
        var fixture = new TFixture();
        var (owner, created) = await CreateGoalAsync(fixture);
        _ = await TransitionAsync(fixture, owner, created, GoalStatus.Ready, "shared-key");

        var rejected = await TryTransitionAsync(fixture, owner, created, GoalStatus.Cancelled, "shared-key");

        rejected.Failure.Kind.ShouldBe(GoalStoreFailureKind.IdempotencyConflict);
    }

    /// <summary>Verifies activating a ready goal requires starting the next attempt.</summary>
    [Fact]
    public async Task TransitionAsync_WhenActivatingWithoutAnAttemptStart_RejectsWithInvalidTransition()
    {
        var fixture = new TFixture();
        var (owner, created) = await CreateGoalAsync(fixture);
        var ready = await TransitionAsync(fixture, owner, created, GoalStatus.Ready, "ready-1");

        var rejected = await TryTransitionAsync(fixture, owner, ready.Record, GoalStatus.Active, "active-1");

        rejected.Failure.Kind.ShouldBe(GoalStoreFailureKind.InvalidTransition);
    }

    /// <summary>Verifies starting an attempt records a running attempt that holds the lease.</summary>
    [Fact]
    public async Task TransitionAsync_WhenActivatingWithAnAttemptStart_RecordsTheRunningAttempt()
    {
        var fixture = new TFixture();
        var (owner, created) = await CreateGoalAsync(fixture);
        var ready = await TransitionAsync(fixture, owner, created, GoalStatus.Ready, "ready-1");

        var active = await ActivateAsync(fixture, owner, ready.Record, "active-1");

        active.Record.Goal.Status.ShouldBe(GoalStatus.Active);
        active.Record.ActiveAttempt.ShouldNotBeNull().Number.ShouldBe(1);
        active.Record.ActiveAttempt.Status.ShouldBe(GoalAttemptStatus.Running);
        active.Record.Goal.ActiveAttemptId.ShouldBe(active.Record.ActiveAttempt.Id);
    }

    /// <summary>Verifies completing settles the attempt and assigns the durable settlement sequence.</summary>
    [Fact]
    public async Task TransitionAsync_WhenCompletingAnActiveGoal_SettlesTheAttemptAndAssignsASettlementSequence()
    {
        var fixture = new TFixture();
        var active = await ActiveGoalAsync(fixture);
        var attempt = active.Record.ActiveAttempt.ShouldNotBeNull();

        var completed = await TransitionAsync(
            fixture,
            active.Owner,
            active.Record,
            GoalStatus.Completed,
            "complete-1",
            new GoalAttemptSettlement(attempt.Id, GoalAttemptStatus.Succeeded, GoalTestData.Outcome(attempt.RunId), GoalTestData.Now.AddMinutes(1)));

        completed.Record.Goal.Status.ShouldBe(GoalStatus.Completed);
        completed.Record.Goal.ActiveAttemptId.ShouldBeNull();
        completed.Record.Attempts[0].Status.ShouldBe(GoalAttemptStatus.Succeeded);
        _ = completed.Record.Attempts[0].Outcome.ShouldNotBeNull();
        _ = completed.Record.SettledSequence.ShouldNotBeNull();
    }

    /// <summary>Verifies leaving an active goal without settling its attempt is refused.</summary>
    [Fact]
    public async Task TransitionAsync_WhenCompletingWithoutSettlingTheAttempt_RejectsWithInvalidTransition()
    {
        var fixture = new TFixture();
        var active = await ActiveGoalAsync(fixture);

        var rejected = await TryTransitionAsync(fixture, active.Owner, active.Record, GoalStatus.Completed, "complete-1");

        rejected.Failure.Kind.ShouldBe(GoalStoreFailureKind.InvalidTransition);
    }

    /// <summary>Verifies a retry creates a new attempt and keeps the failed one as evidence.</summary>
    [Fact]
    public async Task TransitionAsync_WhenRetryingAFailedGoal_CreatesANewAttemptAndKeepsTheFailedEvidence()
    {
        var fixture = new TFixture();
        var active = await ActiveGoalAsync(fixture);
        var first = active.Record.ActiveAttempt.ShouldNotBeNull();
        var failed = await TransitionAsync(
            fixture,
            active.Owner,
            active.Record,
            GoalStatus.Failed,
            "fail-1",
            new GoalAttemptSettlement(first.Id, GoalAttemptStatus.Failed, null, GoalTestData.Now.AddMinutes(1)));
        var ready = await TransitionAsync(fixture, active.Owner, failed.Record, GoalStatus.Ready, "retry-1", reason: GoalTransitionReason.Retried);

        var second = await ActivateAsync(fixture, active.Owner, ready.Record, "active-2");

        second.Record.Attempts.Length.ShouldBe(2);
        second.Record.Attempts[0].Status.ShouldBe(GoalAttemptStatus.Failed);
        second.Record.ActiveAttempt.ShouldNotBeNull().Number.ShouldBe(2);
        ready.Record.SettledSequence.ShouldBeNull();
    }

    /// <summary>Verifies a transition addressed to another agent's goal is refused.</summary>
    [Fact]
    public async Task TransitionAsync_WhenAddressedToAnotherAgentsGoal_RejectsWithScopeMismatch()
    {
        var fixture = new TFixture();
        var (owner, created) = await CreateGoalAsync(fixture);
        var stranger = await fixture.CreateOwnerAsync("tenant", TestContext.Current.CancellationToken);
        var foreign = new GoalTransition(
            created.Goal.Id, stranger.AgentId, stranger.SessionId, stranger.RunId, GoalTestData.NewOperation(),
            GoalStatus.Proposed, GoalStatus.Ready, TransitionActor.Coordinator, GoalTransitionReason.Admitted,
            created.Goal.Version, new IdempotencyKey("ready-foreign"), GoalTestData.Now);

        var result = await fixture.Store.TransitionAsync(
            Factory(fixture).Transition(foreign, null, stranger.Authorization), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<GoalTransitionRejected>().Failure.Kind.ShouldBeOneOf(GoalStoreFailureKind.ScopeMismatch, GoalStoreFailureKind.NotFound);
        (await LoadAsync(fixture, owner, created.Goal.Id)).Goal.Status.ShouldBe(GoalStatus.Proposed);
    }

    /// <summary>Verifies a denied grant leaves the goal unchanged.</summary>
    [Fact]
    public async Task TransitionAsync_WhenTheGrantIsDenied_LeavesTheGoalUnchanged()
    {
        var fixture = new TFixture();
        var (owner, created) = await CreateGoalAsync(fixture);
        var request = Factory(fixture).Transition(GoalTestData.Transition(created, GoalStatus.Ready, "ready-1"), null, owner.Authorization);
        fixture.Grants.ForcedStatus = GrantConsumptionStatus.Revoked;

        var result = await fixture.Store.TransitionAsync(request, TestContext.Current.CancellationToken);
        fixture.Grants.ForcedStatus = null;

        result.ShouldBeOfType<GoalTransitionRejected>().Failure.Kind.ShouldBe(GoalStoreFailureKind.Denied);
        (await LoadAsync(fixture, owner, created.Goal.Id)).Goal.Status.ShouldBe(GoalStatus.Proposed);
    }

    /// <summary>Verifies concurrent transitions from one observed version apply exactly once.</summary>
    [Fact]
    public async Task TransitionAsync_WhenTransitionsRaceFromOneVersion_ExactlyOneIsApplied()
    {
        var fixture = new TFixture();
        var (owner, created) = await CreateGoalAsync(fixture);
        var requests = Enumerable.Range(0, 6)
            .Select(index => Factory(fixture).Transition(
                GoalTestData.Transition(created, GoalStatus.Ready, $"race-{index}"), null, owner.Authorization))
            .ToArray();

        var results = await Task.WhenAll(requests.Select(request => fixture.Store.TransitionAsync(request, TestContext.Current.CancellationToken).AsTask()));

        results.OfType<GoalTransitioned>().Count().ShouldBe(1);
        results.OfType<GoalTransitionRejected>().ShouldAllBe(rejected => rejected.Failure.Kind == GoalStoreFailureKind.VersionConflict);
    }

    /// <summary>Verifies children page in recorded ordinal order with a stable cursor.</summary>
    [Fact]
    public async Task ReadChildrenAsync_WhenPaging_ReturnsChildrenInRecordedOrdinalOrder()
    {
        var fixture = new TFixture();
        var parent = await ActiveGoalAsync(fixture, new GoalBudget(10, 50, 5));
        var childIds = new List<GoalId>();
        for (var index = 0; index < 3; index++)
        {
            var child = GoalTestData.Goal(parent.Owner.AgentId, parent.Owner.SessionId, parent.Owner.RunId, parent.Record.Goal.Id);
            childIds.Add(child.Id);
            var created = await CreateAsync(fixture, parent.Owner, child, $"child-{index}");
            created.Record.ChildOrdinal.ShouldBe(index + 1);
        }

        var first = (await fixture.Store.ReadChildrenAsync(
            Factory(fixture).Children(parent.Record.Goal.Id, 0, 2, parent.Owner.Authorization), TestContext.Current.CancellationToken))
            .ShouldBeOfType<GoalPage>();
        var second = (await fixture.Store.ReadChildrenAsync(
            Factory(fixture).Children(parent.Record.Goal.Id, first.Next.ShouldNotBeNull(), 2, parent.Owner.Authorization), TestContext.Current.CancellationToken))
            .ShouldBeOfType<GoalPage>();

        first.Items.Select(item => item.Goal.Id).ShouldBe(childIds.Take(2));
        first.Next.ShouldBe(2);
        second.Items.Select(item => item.Goal.Id).ShouldBe(childIds.Skip(2));
        second.Next.ShouldBeNull();
    }

    /// <summary>Verifies the parent's child ceiling is enforced at creation.</summary>
    [Fact]
    public async Task CreateAsync_WhenTheParentChildCeilingIsReached_RejectsWithLimitExceeded()
    {
        var fixture = new TFixture();
        var parent = await ActiveGoalAsync(fixture, new GoalBudget(10, 50, 1));
        _ = await CreateAsync(
            fixture, parent.Owner, GoalTestData.Goal(parent.Owner.AgentId, parent.Owner.SessionId, parent.Owner.RunId, parent.Record.Goal.Id), "child-1");

        var rejected = await TryCreateAsync(
            fixture, parent.Owner, GoalTestData.Goal(parent.Owner.AgentId, parent.Owner.SessionId, parent.Owner.RunId, parent.Record.Goal.Id), "child-2");

        rejected.Failure.Kind.ShouldBe(GoalStoreFailureKind.LimitExceeded);
    }

    /// <summary>Verifies a parent that is not active is not open to children.</summary>
    [Fact]
    public async Task CreateAsync_WhenTheParentIsNotActive_RejectsWithLimitExceeded()
    {
        var fixture = new TFixture();
        var (owner, parent) = await CreateGoalAsync(fixture);

        var rejected = await TryCreateAsync(
            fixture, owner, GoalTestData.Goal(owner.AgentId, owner.SessionId, owner.RunId, parent.Goal.Id), "child-1");

        rejected.Failure.Kind.ShouldBe(GoalStoreFailureKind.LimitExceeded);
    }

    /// <summary>Verifies a child whose parent does not exist is refused.</summary>
    [Fact]
    public async Task CreateAsync_WhenTheParentDoesNotExist_RejectsWithNotFound()
    {
        var fixture = new TFixture();
        var owner = await fixture.CreateOwnerAsync("tenant", TestContext.Current.CancellationToken);

        var rejected = await TryCreateAsync(
            fixture, owner, GoalTestData.Goal(owner.AgentId, owner.SessionId, owner.RunId, new GoalId(Guid.NewGuid())), "child-1");

        rejected.Failure.Kind.ShouldBe(GoalStoreFailureKind.NotFound);
    }

    /// <summary>Verifies a caller scoped to another agent cannot list a goal's children.</summary>
    [Fact]
    public async Task ReadChildrenAsync_WhenCallerScopeOwnsAnotherAgent_RejectsWithScopeMismatch()
    {
        var fixture = new TFixture();
        var parent = await ActiveGoalAsync(fixture);
        var stranger = await fixture.CreateOwnerAsync("tenant", TestContext.Current.CancellationToken);

        var result = await fixture.Store.ReadChildrenAsync(
            Factory(fixture).Children(parent.Record.Goal.Id, 0, 5, stranger.Authorization), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<GoalPageRejected>().Failure.Kind.ShouldBeOneOf(GoalStoreFailureKind.ScopeMismatch, GoalStoreFailureKind.NotFound);
    }

    /// <summary>Verifies a delegated child keeps its captured delegation when the adapter claims to.</summary>
    [Fact]
    public async Task LoadAsync_WhenAChildWasDelegated_ReturnsTheCapturedDelegation()
    {
        var fixture = new TFixture();
        if (!fixture.PreservesDelegation)
        {
            return;
        }

        var parent = await ActiveGoalAsync(fixture);
        var child = GoalTestData.Goal(parent.Owner.AgentId, parent.Owner.SessionId, parent.Owner.RunId, parent.Record.Goal.Id);
        var delegation = GoalTestData.Delegation(
            parent.Record.Goal, parent.Record.ActiveAttempt!.Id, parent.Owner.RunId, GoalTestData.NewAgent(), "delegation-1", parent.Owner.Authorization);
        _ = await CreateAsync(fixture, parent.Owner, child, "child-1", delegation);

        var loaded = await LoadAsync(fixture, parent.Owner, child.Id);

        loaded.Delegation.ShouldBe(delegation);
    }

    /// <summary>Verifies intent discovery lists open delegated children in creation order across tenants.</summary>
    [Fact]
    public async Task ReadIntentsAsync_WhenChildrenAreOpen_ListsThemInCreationOrderAcrossTenants()
    {
        var fixture = new TFixture();
        if (fixture.IntentScanner is not { } scanner || !fixture.PreservesDelegation)
        {
            return;
        }

        var expected = new List<GoalId>();
        foreach (var tenant in new[] { "tenant-a", "tenant-b" })
        {
            var parent = await ActiveGoalAsync(fixture, tenant: tenant);
            var child = GoalTestData.Goal(parent.Owner.AgentId, parent.Owner.SessionId, parent.Owner.RunId, parent.Record.Goal.Id, GoalStatus.Ready);
            var delegation = GoalTestData.Delegation(
                parent.Record.Goal, parent.Record.ActiveAttempt!.Id, parent.Owner.RunId, GoalTestData.NewAgent(), $"delegation-{tenant}", parent.Owner.Authorization);
            _ = await CreateAsync(fixture, parent.Owner, child, $"child-{tenant}", delegation);
            expected.Add(child.Id);
        }

        var page = (await fixture.Store.ReadIntentsAsync(new GoalIntentScanRequest(scanner, 0, 10), TestContext.Current.CancellationToken))
            .ShouldBeOfType<GoalPage>();

        page.Items.Where(item => expected.Contains(item.Goal.Id)).Select(item => item.Goal.Id).ShouldBe(expected);
        page.Items.ShouldAllBe(item => item.Delegation != null && item.Goal.Status == GoalStatus.Ready);
    }

    /// <summary>Verifies an unconfigured scanner cannot enumerate intents.</summary>
    [Fact]
    public async Task ReadIntentsAsync_WhenTheScannerIsNotConfigured_IsRefused()
    {
        var fixture = new TFixture();
        if (fixture.IntentScanner is null)
        {
            return;
        }

        var result = await fixture.Store.ReadIntentsAsync(
            new GoalIntentScanRequest(new ComponentId("not-configured"), 0, 10), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<GoalPageRejected>().Failure.Kind.ShouldBe(GoalStoreFailureKind.Denied);
    }

    /// <summary>Verifies a store without intent discovery reports it as unavailable rather than returning an empty page.</summary>
    [Fact]
    public async Task ReadIntentsAsync_WhenTheAdapterOffersNoDiscovery_ReportsUnavailable()
    {
        var fixture = new TFixture();
        if (fixture.IntentScanner is not null)
        {
            return;
        }

        var result = await fixture.Store.ReadIntentsAsync(
            new GoalIntentScanRequest(new ComponentId("any"), 0, 10), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<GoalPageRejected>().Failure.Kind.ShouldBe(GoalStoreFailureKind.Unavailable);
    }

    /// <summary>Verifies a cancelled token stops the operation before it writes.</summary>
    [Fact]
    public async Task CreateAsync_WhenTheTokenIsAlreadyCancelled_ThrowsAndWritesNothing()
    {
        var fixture = new TFixture();
        var owner = await fixture.CreateOwnerAsync("tenant", TestContext.Current.CancellationToken);
        var goal = GoalTestData.Goal(owner.AgentId, owner.SessionId, owner.RunId);
        var request = Factory(fixture).Create(goal, owner.Authorization, "create-1");
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await fixture.Store.CreateAsync(request, cancelled.Token));

        (await TryLoadAsync(fixture, owner, goal.Id)).Failure.Kind.ShouldBe(GoalStoreFailureKind.NotFound);
    }

    /// <summary>Verifies acknowledged goals, attempts, and transitions survive reopening the store.</summary>
    [Fact]
    public async Task LoadAsync_AfterReopeningTheStore_ReturnsEveryAcknowledgedRecord()
    {
        var fixture = new TFixture();
        if (!fixture.Capabilities.SupportsDurability)
        {
            return;
        }

        var active = await ActiveGoalAsync(fixture);
        var reopened = await fixture.ReopenAsync(TestContext.Current.CancellationToken);

        var loaded = (await reopened.LoadAsync(
            Factory(fixture, reopened).Load(active.Record.Goal.Id, active.Owner.Authorization), TestContext.Current.CancellationToken))
            .ShouldBeOfType<GoalLoaded>().Record;

        loaded.Goal.Status.ShouldBe(GoalStatus.Active);
        loaded.Goal.Version.ShouldBe(active.Record.Goal.Version);
        loaded.Attempts.Length.ShouldBe(1);
        loaded.Transitions.Length.ShouldBe(2);
        loaded.Sequence.ShouldBe(active.Record.Sequence);
    }

    private static GoalRequestFactory Factory(TFixture fixture) => Factory(fixture, fixture.Store);

    private static GoalRequestFactory Factory(TFixture fixture, IGoalStore store) => new(fixture.Grants, store.Descriptor.SecurityAudience);

    private static async Task<GoalCreated> CreateAsync(
        TFixture fixture, GoalConformanceOwner owner, AgentGoal goal, string key, DelegationRequest? delegation = null) =>
        (await fixture.Store.CreateAsync(
            Factory(fixture).Create(goal, owner.Authorization, key, delegation), TestContext.Current.CancellationToken))
        .ShouldBeOfType<GoalCreated>();

    private static async Task<GoalCreateRejected> TryCreateAsync(
        TFixture fixture, GoalConformanceOwner owner, AgentGoal goal, string key) =>
        (await fixture.Store.CreateAsync(
            Factory(fixture).Create(goal, owner.Authorization, key), TestContext.Current.CancellationToken))
        .ShouldBeOfType<GoalCreateRejected>();

    private static async Task<GoalRecord> LoadAsync(TFixture fixture, GoalConformanceOwner owner, GoalId goalId) =>
        (await fixture.Store.LoadAsync(
            Factory(fixture).Load(goalId, owner.Authorization), TestContext.Current.CancellationToken))
        .ShouldBeOfType<GoalLoaded>().Record;

    private static async Task<GoalLoadRejected> TryLoadAsync(TFixture fixture, GoalConformanceOwner owner, GoalId goalId) =>
        (await fixture.Store.LoadAsync(
            Factory(fixture).Load(goalId, owner.Authorization), TestContext.Current.CancellationToken))
        .ShouldBeOfType<GoalLoadRejected>();

    private static async Task<(GoalConformanceOwner Owner, GoalRecord Record)> CreateGoalAsync(TFixture fixture, GoalBudget? budget = null, string tenant = "tenant")
    {
        var owner = await fixture.CreateOwnerAsync(tenant, TestContext.Current.CancellationToken);
        var created = await CreateAsync(fixture, owner, GoalTestData.Goal(owner.AgentId, owner.SessionId, owner.RunId, budget: budget), "create-root");
        return (owner, created.Record);
    }

    private static async Task<GoalTransitioned> TransitionAsync(
        TFixture fixture,
        GoalConformanceOwner owner,
        GoalRecord record,
        GoalStatus to,
        string key,
        GoalAttemptChange? attempt = null,
        GoalTransitionReason reason = GoalTransitionReason.Admitted) =>
        (await fixture.Store.TransitionAsync(
            Factory(fixture).Transition(GoalTestData.Transition(record, to, key, reason), attempt, owner.Authorization),
            TestContext.Current.CancellationToken))
        .ShouldBeOfType<GoalTransitioned>();

    private static async Task<GoalTransitionRejected> TryTransitionAsync(
        TFixture fixture, GoalConformanceOwner owner, GoalRecord record, GoalStatus to, string key) =>
        (await fixture.Store.TransitionAsync(
            Factory(fixture).Transition(GoalTestData.Transition(record, to, key), null, owner.Authorization),
            TestContext.Current.CancellationToken))
        .ShouldBeOfType<GoalTransitionRejected>();

    private static Task<GoalTransitioned> ActivateAsync(TFixture fixture, GoalConformanceOwner owner, GoalRecord ready, string key) =>
        TransitionAsync(
            fixture,
            owner,
            ready,
            GoalStatus.Active,
            key,
            new GoalAttemptStart(GoalTestData.Attempt(ready.Goal.Id, ready.Attempts.Length + 1, GoalTestData.NewAgent(), GoalTestData.NewSession(), GoalTestData.NewRun())),
            GoalTransitionReason.AttemptStarted);

    private static async Task<(GoalConformanceOwner Owner, GoalRecord Record)> ActiveGoalAsync(
        TFixture fixture, GoalBudget? budget = null, string tenant = "tenant")
    {
        var (owner, created) = await CreateGoalAsync(fixture, budget, tenant);
        var ready = await TransitionAsync(fixture, owner, created, GoalStatus.Ready, "root-ready");
        var active = await ActivateAsync(fixture, owner, ready.Record, "root-active");
        return (owner, active.Record);
    }
}
