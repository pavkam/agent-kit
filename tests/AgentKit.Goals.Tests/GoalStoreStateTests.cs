// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

/// <summary>Verifies the in-memory goal index that the in-memory and JSON adapters share.</summary>
public sealed class GoalStoreStateTests
{
    private static readonly TestGoalGrants _grants = new();
    private static readonly TenantId _tenant = new("tenant");
    private readonly AgentId _agent = GoalTestData.NewAgent();
    private readonly SessionId _session = GoalTestData.NewSession();
    private readonly RunId _run = GoalTestData.NewRun();

    private SecurityAuthorizationContext Authorization() => GoalTestData.Authorization(_agent, _session, _run);

    private GoalCreateRequest CreateRequest(AgentGoal goal, string key, DelegationRequest? delegation = null) =>
        new GoalRequestFactory(_grants, new ComponentId("store")).Create(goal, Authorization(), key, delegation);

    private static GoalRecord Commit(GoalStoreState state, GoalCreateRequest request)
    {
        var plan = state.PlanCreate(_tenant, request);
        plan.Kind.ShouldBe(GoalReductionKind.Applied);
        state.Commit(plan);
        return plan.Record!;
    }

    private GoalRecord ActiveRoot(GoalStoreState state, GoalBudget? budget = null)
    {
        var created = Commit(state, CreateRequest(GoalTestData.Goal(_agent, _session, _run, budget: budget), "root"));
        var factory = new GoalRequestFactory(_grants, new ComponentId("store"));
        var ready = factory.Transition(GoalTestData.Transition(created, GoalStatus.Ready, "ready"), null, Authorization());
        var readyPlan = state.PlanTransition(_tenant, ready);
        state.Commit(readyPlan);
        var attempt = GoalTestData.Attempt(created.Goal.Id, 1, GoalTestData.NewAgent(), GoalTestData.NewSession(), GoalTestData.NewRun());
        var active = factory.Transition(GoalTestData.Transition(readyPlan.Record!, GoalStatus.Active, "active"), new GoalAttemptStart(attempt), Authorization());
        var activePlan = state.PlanTransition(_tenant, active);
        state.Commit(activePlan);
        return activePlan.Record!;
    }

    [Fact]
    public void PlanCreate_WhenPlannedButNotCommitted_LeavesStateUnchanged()
    {
        var state = new GoalStoreState();

        var plan = state.PlanCreate(_tenant, CreateRequest(GoalTestData.Goal(_agent, _session, _run), "k"));

        plan.Kind.ShouldBe(GoalReductionKind.Applied);
        state.Count.ShouldBe(0);
    }

    [Fact]
    public void PlanCreate_WhenTheKeyRepeatsWithAnEquivalentRequest_ReplaysTheStoredGoal()
    {
        var state = new GoalStoreState();
        var goal = GoalTestData.Goal(_agent, _session, _run);
        var first = Commit(state, CreateRequest(goal, "k"));

        var replay = state.PlanCreate(_tenant, CreateRequest(goal, "k"));

        replay.Kind.ShouldBe(GoalReductionKind.Replayed);
        replay.Record.ShouldBe(first);
    }

    [Fact]
    public void PlanCreate_WhenTheSameKeyIsUsedInAnotherTenant_CreatesAnIndependentGoal()
    {
        var state = new GoalStoreState();
        var goal = GoalTestData.Goal(_agent, _session, _run);
        _ = Commit(state, CreateRequest(goal, "k"));

        var other = state.PlanCreate(new TenantId("other"), CreateRequest(goal, "k"));

        other.Kind.ShouldBe(GoalReductionKind.Applied);
        state.Find(new TenantId("other"), goal.Id).ShouldBeNull();
    }

    [Fact]
    public void PlanCreate_WhenChildrenAreCreated_AssignsAppendOnlyOrdinalsAndCreationSequences()
    {
        var state = new GoalStoreState();
        var root = ActiveRoot(state);

        var first = Commit(state, CreateRequest(GoalTestData.Goal(_agent, _session, _run, root.Goal.Id), "c1"));
        var second = Commit(state, CreateRequest(GoalTestData.Goal(_agent, _session, _run, root.Goal.Id), "c2"));

        first.ChildOrdinal.ShouldBe(1);
        second.ChildOrdinal.ShouldBe(2);
        second.Sequence.ShouldBeGreaterThan(first.Sequence);
    }

    [Fact]
    public void PlanTransition_WhenTheGoalDoesNotExistOrIsOwnedElsewhere_ReportsTheTypedFailure()
    {
        var state = new GoalStoreState();
        var created = Commit(state, CreateRequest(GoalTestData.Goal(_agent, _session, _run), "k"));
        var factory = new GoalRequestFactory(_grants, new ComponentId("store"));
        var stranger = GoalTestData.NewAgent();
        var foreign = new GoalTransition(
            created.Goal.Id, stranger, _session, _run, GoalTestData.NewOperation(), GoalStatus.Proposed, GoalStatus.Ready,
            TransitionActor.Agent, GoalTransitionReason.Admitted, created.Goal.Version, new IdempotencyKey("x"), GoalTestData.Now);

        state.PlanTransition(_tenant, factory.Transition(GoalTestData.Transition(new GoalRecord(GoalTestData.Goal(_agent, _session, _run), [], [], null, 1, null, null), GoalStatus.Ready, "m"), null, Authorization()))
            .Failure!.Kind.ShouldBe(GoalStoreFailureKind.NotFound);
        state.PlanTransition(_tenant, factory.Transition(foreign, null, GoalTestData.Authorization(stranger, _session, _run)))
            .Failure!.Kind.ShouldBe(GoalStoreFailureKind.ScopeMismatch);
    }

    [Fact]
    public void Restore_WhenRebuildingFromSnapshots_RestoresIndexesSequencesAndSettlementOrder()
    {
        var source = new GoalStoreState();
        var root = ActiveRoot(source);
        var child = Commit(source, CreateRequest(GoalTestData.Goal(_agent, _session, _run, root.Goal.Id), "c1"));

        var rebuilt = new GoalStoreState();
        rebuilt.Restore(_tenant, "root", source.Find(_tenant, root.Goal.Id)!);
        rebuilt.Restore(_tenant, "c1", child);

        rebuilt.Count.ShouldBe(2);
        rebuilt.PlanCreate(_tenant, CreateRequest(GoalTestData.Goal(_agent, _session, _run, root.Goal.Id), "c2")).Record!.ChildOrdinal.ShouldBe(2);
        rebuilt.PlanCreate(_tenant, CreateRequest(child.Goal, "c1")).Kind.ShouldBe(GoalReductionKind.Replayed);
    }

    [Fact]
    public void ReadIntents_WhenChildrenAreDelegated_ListsOnlyOpenDelegatedChildrenInCreationOrder()
    {
        var state = new GoalStoreState();
        var root = ActiveRoot(state, new GoalBudget(10, 10, 5));
        var attempt = root.ActiveAttempt!;
        var ready = GoalTestData.Goal(_agent, _session, _run, root.Goal.Id, GoalStatus.Ready);
        var proposed = GoalTestData.Goal(_agent, _session, _run, root.Goal.Id);
        var undelegated = GoalTestData.Goal(_agent, _session, _run, root.Goal.Id, GoalStatus.Ready);
        var readyRecord = Commit(state, CreateRequest(ready, "c1", GoalTestData.Delegation(root.Goal, attempt.Id, _run, GoalTestData.NewAgent(), "d1", Authorization())));
        _ = Commit(state, CreateRequest(proposed, "c2", GoalTestData.Delegation(root.Goal, attempt.Id, _run, GoalTestData.NewAgent(), "d2", Authorization())));
        _ = Commit(state, CreateRequest(undelegated, "c3"));

        var page = state.ReadIntents(new GoalIntentScanRequest(new ComponentId("w"), 0, 10)).ShouldBeOfType<GoalPage>();

        page.Items.Select(static item => item.Goal.Status).ShouldBe([GoalStatus.Ready]);
        page.Items[0].Goal.Id.ShouldBe(readyRecord.Goal.Id);
    }

    [Fact]
    public void ReadIntents_WhenMoreThanOnePageExists_ReturnsACursorThatContinuesWithoutRepeats()
    {
        var state = new GoalStoreState();
        var root = ActiveRoot(state, new GoalBudget(10, 10, 5));
        var attempt = root.ActiveAttempt!;
        for (var index = 0; index < 3; index++)
        {
            var child = GoalTestData.Goal(_agent, _session, _run, root.Goal.Id, GoalStatus.Ready);
            _ = Commit(state, CreateRequest(child, $"c{index}", GoalTestData.Delegation(root.Goal, attempt.Id, _run, GoalTestData.NewAgent(), $"d{index}", Authorization())));
        }

        var first = state.ReadIntents(new GoalIntentScanRequest(new ComponentId("w"), 0, 2)).ShouldBeOfType<GoalPage>();
        var second = state.ReadIntents(new GoalIntentScanRequest(new ComponentId("w"), first.Next!.Value, 2)).ShouldBeOfType<GoalPage>();

        first.Items.Length.ShouldBe(2);
        second.Items.Length.ShouldBe(1);
        second.Next.ShouldBeNull();
        first.Items.Concat(second.Items).Select(static item => item.Goal.Id).Distinct().Count().ShouldBe(3);
    }
}
