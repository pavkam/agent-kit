// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

/// <summary>Verifies the shared goal reducer that every store adapter runs.</summary>
public sealed class GoalRecordReducerTests
{
    private static readonly TestGoalGrants _grants = new();
    private readonly AgentId _agent = GoalTestData.NewAgent();
    private readonly SessionId _session = GoalTestData.NewSession();
    private readonly RunId _run = GoalTestData.NewRun();

    private GoalRecord Created(GoalStatus status = GoalStatus.Proposed, GoalBudget? budget = null)
    {
        var goal = GoalTestData.Goal(_agent, _session, _run, status: status, budget: budget);
        var request = new GoalCreateRequest(goal, null, new IdempotencyKey("create"), Grant());
        return GoalRecordReducer.Create(request, 1, null);
    }

    private SecurityGrant Grant() => _grants.Issue(
        new ComponentId("store"), GoalTestData.Authorization(_agent, _session, _run), SecurityOperationKind.StateMutation,
        SecurityEffect.Mutate, [new ProtectedResource(ProtectedResourceKind.ApplicationState, "r")], new InputFingerprint("f"));

    private GoalTransitionRequest Request(GoalRecord record, GoalStatus to, string key, GoalAttemptChange? change = null) =>
        new(GoalTestData.Profile, GoalTestData.Transition(record, to, key), change, Grant());

    private static GoalRecord Applied(GoalReduction reduction) => reduction.Kind == GoalReductionKind.Applied
        ? reduction.Record!
        : throw new InvalidOperationException($"Expected an applied reduction but got {reduction.Kind}: {reduction.Failure?.SafeMessage}");

    private GoalRecord Active(out GoalAttempt attempt)
    {
        var created = Created();
        var ready = Applied(GoalRecordReducer.ApplyTransition(created, Request(created, GoalStatus.Ready, "r"), 1));
        attempt = GoalTestData.Attempt(ready.Goal.Id, 1, GoalTestData.NewAgent(), GoalTestData.NewSession(), GoalTestData.NewRun());
        return Applied(GoalRecordReducer.ApplyTransition(ready, Request(ready, GoalStatus.Active, "a", new GoalAttemptStart(attempt)), 1));
    }

    [Fact]
    public void Create_WhenTheCallerSuppliesAnyVersion_NormalizesToTheInitialVersion()
    {
        var goal = GoalTestData.Goal(_agent, _session, _run).WithState(GoalStatus.Proposed, null, new VersionToken("99"));
        var request = new GoalCreateRequest(goal, null, new IdempotencyKey("k"), Grant());

        var record = GoalRecordReducer.Create(request, 5, null);

        record.Goal.Version.ShouldBe(GoalRecordReducer.InitialVersion);
        record.Sequence.ShouldBe(5);
        record.Attempts.ShouldBeEmpty();
        record.Transitions.ShouldBeEmpty();
    }

    [Fact]
    public void NextVersion_WhenGivenADecimalToken_IncrementsIt() => GoalRecordReducer.NextVersion(new VersionToken("9")).ShouldBe(new VersionToken("10"));

    [Theory]
    [InlineData(GoalStatus.Completed, true)]
    [InlineData(GoalStatus.Failed, true)]
    [InlineData(GoalStatus.Cancelled, true)]
    [InlineData(GoalStatus.Blocked, false)]
    [InlineData(GoalStatus.Ready, false)]
    [InlineData(GoalStatus.Active, false)]
    public void AssignsSettledSequence_WhenStatusIsEnumerated_IsTrueOnlyForCompletedFailedAndCancelled(GoalStatus status, bool expected) =>
        GoalRecordReducer.AssignsSettledSequence(status).ShouldBe(expected);

    [Fact]
    public void IsCreationEquivalent_WhenOnlyLaterStateDiffers_IsTrueAndWhenACreationFactDiffersIsFalse()
    {
        var goal = GoalTestData.Goal(_agent, _session, _run);
        var request = new GoalCreateRequest(goal, null, new IdempotencyKey("k"), Grant());
        var stored = GoalRecordReducer.Create(request, 1, null);
        var advanced = Applied(GoalRecordReducer.ApplyTransition(stored, Request(stored, GoalStatus.Ready, "r"), 1));
        var different = new GoalCreateRequest(GoalTestData.Goal(_agent, _session, _run), null, new IdempotencyKey("k"), Grant());

        GoalRecordReducer.IsCreationEquivalent(advanced, request).ShouldBeTrue();
        GoalRecordReducer.IsCreationEquivalent(stored, different).ShouldBeFalse();
    }

    [Fact]
    public void ValidateParent_WhenParentIsOpenAndHasRoom_AcceptsTheChild()
    {
        var parent = Active(out _);
        var child = GoalTestData.Goal(_agent, _session, _run, parent.Goal.Id);

        GoalRecordReducer.ValidateParent(parent, child, 0).ShouldBeNull();
    }

    [Fact]
    public void ValidateParent_WhenTheParentIsMissing_ReportsNotFound() =>
        GoalRecordReducer.ValidateParent(null, GoalTestData.Goal(_agent, _session, _run, new GoalId(Guid.NewGuid())), 0)!.Kind.ShouldBe(GoalStoreFailureKind.NotFound);

    [Fact]
    public void ValidateParent_WhenTheChildIsOwnedElsewhere_ReportsScopeMismatch()
    {
        var parent = Active(out _);
        var child = GoalTestData.Goal(GoalTestData.NewAgent(), _session, _run, parent.Goal.Id);

        GoalRecordReducer.ValidateParent(parent, child, 0)!.Kind.ShouldBe(GoalStoreFailureKind.ScopeMismatch);
    }

    [Fact]
    public void ValidateParent_WhenTheParentIsNotOpenOrFull_ReportsLimitExceeded()
    {
        var notOpen = Created();
        var parent = Active(out _);
        var child = GoalTestData.Goal(_agent, _session, _run, parent.Goal.Id);

        GoalRecordReducer.ValidateParent(notOpen, GoalTestData.Goal(_agent, _session, _run, notOpen.Goal.Id), 0)!.Kind.ShouldBe(GoalStoreFailureKind.LimitExceeded);
        GoalRecordReducer.ValidateParent(parent, child, parent.Goal.Budget.MaximumChildren)!.Kind.ShouldBe(GoalStoreFailureKind.LimitExceeded);
    }

    [Fact]
    public void ApplyTransition_WhenPlainTransitionMatches_IncrementsVersionAndAppendsTheTransition()
    {
        var created = Created();

        var next = Applied(GoalRecordReducer.ApplyTransition(created, Request(created, GoalStatus.Ready, "r"), 1));

        next.Goal.Status.ShouldBe(GoalStatus.Ready);
        next.Goal.Version.Value.ShouldBe("2");
        next.Transitions.Length.ShouldBe(1);
        created.Transitions.ShouldBeEmpty();
    }

    [Fact]
    public void ApplyTransition_WhenVersionOrObservedStatusIsStale_RejectsWithVersionConflict()
    {
        var created = Created();
        var stale = new GoalTransition(
            created.Goal.Id, _agent, _session, _run, GoalTestData.NewOperation(), GoalStatus.Proposed, GoalStatus.Ready,
            TransitionActor.Agent, GoalTransitionReason.Admitted, new VersionToken("7"), new IdempotencyKey("r"), GoalTestData.Now);

        GoalRecordReducer.ApplyTransition(created, stale, null, 1).Failure!.Kind.ShouldBe(GoalStoreFailureKind.VersionConflict);
    }

    [Fact]
    public void ApplyTransition_WhenTheSameTransitionIsReplayed_ReturnsTheStoredRecordAsAReplay()
    {
        var created = Created();
        var next = Applied(GoalRecordReducer.ApplyTransition(created, Request(created, GoalStatus.Ready, "r"), 1));

        var replay = GoalRecordReducer.ApplyTransition(next, next.Transitions[0], null, 1);

        replay.Kind.ShouldBe(GoalReductionKind.Replayed);
        replay.Record.ShouldBeSameAs(next);
    }

    [Fact]
    public void ApplyTransition_WhenTheKeyIsReusedForAnotherChange_RejectsWithIdempotencyConflict()
    {
        var created = Created();
        var next = Applied(GoalRecordReducer.ApplyTransition(created, Request(created, GoalStatus.Ready, "r"), 1));

        GoalRecordReducer.ApplyTransition(next, Request(next, GoalStatus.Cancelled, "r").Transition, null, 1).Failure!.Kind.ShouldBe(GoalStoreFailureKind.IdempotencyConflict);
    }

    [Fact]
    public void ApplyTransition_WhenActivatingWithAWrongAttempt_RejectsWithInvalidTransition()
    {
        var created = Created();
        var ready = Applied(GoalRecordReducer.ApplyTransition(created, Request(created, GoalStatus.Ready, "r"), 1));
        var wrongNumber = GoalTestData.Attempt(ready.Goal.Id, 2, _agent, _session, _run);
        var wrongGoal = GoalTestData.Attempt(new GoalId(Guid.NewGuid()), 1, _agent, _session, _run);

        GoalRecordReducer.ApplyTransition(ready, Request(ready, GoalStatus.Active, "a1", new GoalAttemptStart(wrongNumber)), 1).Failure!.Kind.ShouldBe(GoalStoreFailureKind.InvalidTransition);
        GoalRecordReducer.ApplyTransition(ready, Request(ready, GoalStatus.Active, "a2", new GoalAttemptStart(wrongGoal)), 1).Failure!.Kind.ShouldBe(GoalStoreFailureKind.InvalidTransition);
        GoalRecordReducer.ApplyTransition(ready, Request(ready, GoalStatus.Active, "a3"), 1).Failure!.Kind.ShouldBe(GoalStoreFailureKind.InvalidTransition);
    }

    [Fact]
    public void ApplyTransition_WhenStartingTheFirstAttempt_RecordsItAsTheActiveAttempt()
    {
        var active = Active(out var attempt);

        active.Goal.Status.ShouldBe(GoalStatus.Active);
        active.Goal.ActiveAttemptId.ShouldBe(attempt.Id);
        active.ActiveAttempt.ShouldBe(attempt);
    }

    [Fact]
    public void ApplyTransition_WhenCompletingWithMatchingSettlement_SettlesTheAttemptAndAssignsTheSequence()
    {
        var active = Active(out var attempt);
        var settlement = new GoalAttemptSettlement(attempt.Id, GoalAttemptStatus.Succeeded, GoalTestData.Outcome(attempt.RunId), GoalTestData.Now.AddMinutes(1));

        var done = Applied(GoalRecordReducer.ApplyTransition(active, Request(active, GoalStatus.Completed, "c", settlement), 41));

        done.Goal.ActiveAttemptId.ShouldBeNull();
        done.Attempts[0].Status.ShouldBe(GoalAttemptStatus.Succeeded);
        done.Attempts[0].EndedAt.ShouldBe(GoalTestData.Now.AddMinutes(1));
        done.SettledSequence.ShouldBe(41);
    }

    [Fact]
    public void ApplyTransition_WhenTheSettlementDoesNotMatch_RejectsWithInvalidTransition()
    {
        var active = Active(out var attempt);
        var failedOutcome = new GoalOutcomeReference(attempt.RunId, DelegationStatus.Failed, null, [], GoalBudgetUsage.None, SideEffectCertainty.Unknown);

        GoalRecordReducer.ApplyTransition(active, Request(active, GoalStatus.Completed, "c1", new GoalAttemptSettlement(attempt.Id, GoalAttemptStatus.Failed, null, GoalTestData.Now)), 1).Failure!.Kind.ShouldBe(GoalStoreFailureKind.InvalidTransition);
        GoalRecordReducer.ApplyTransition(active, Request(active, GoalStatus.Completed, "c2", new GoalAttemptSettlement(attempt.Id, GoalAttemptStatus.Succeeded, failedOutcome, GoalTestData.Now)), 1).Failure!.Kind.ShouldBe(GoalStoreFailureKind.InvalidTransition);
        GoalRecordReducer.ApplyTransition(active, Request(active, GoalStatus.Completed, "c3", new GoalAttemptSettlement(new GoalAttemptId(Guid.NewGuid()), GoalAttemptStatus.Succeeded, GoalTestData.Outcome(attempt.RunId), GoalTestData.Now)), 1).Failure!.Kind.ShouldBe(GoalStoreFailureKind.InvalidTransition);
        GoalRecordReducer.ApplyTransition(active, Request(active, GoalStatus.Completed, "c4", new GoalAttemptSettlement(attempt.Id, GoalAttemptStatus.Succeeded, GoalTestData.Outcome(attempt.RunId), GoalTestData.Now.AddHours(-5))), 1).Failure!.Kind.ShouldBe(GoalStoreFailureKind.InvalidTransition);
    }

    [Fact]
    public void ApplyTransition_WhenParkingAndResuming_KeepsTheRunningAttemptAndRejectsAttemptChanges()
    {
        var active = Active(out var attempt);

        var waiting = Applied(GoalRecordReducer.ApplyTransition(active, Request(active, GoalStatus.Waiting, "w"), 1));
        var resumed = Applied(GoalRecordReducer.ApplyTransition(waiting, Request(waiting, GoalStatus.Active, "u"), 1));

        waiting.Goal.ActiveAttemptId.ShouldBe(attempt.Id);
        resumed.Goal.ActiveAttemptId.ShouldBe(attempt.Id);
        GoalRecordReducer.ApplyTransition(active, Request(active, GoalStatus.Waiting, "w2", new GoalAttemptStart(attempt)), 1).Failure!.Kind.ShouldBe(GoalStoreFailureKind.InvalidTransition);
    }

    [Fact]
    public void ApplyTransition_WhenBlockedAfterActivity_SettlesTheAttemptButAssignsNoSettlementSequence()
    {
        var active = Active(out var attempt);

        var blocked = Applied(GoalRecordReducer.ApplyTransition(
            active, Request(active, GoalStatus.Blocked, "b", new GoalAttemptSettlement(attempt.Id, GoalAttemptStatus.Blocked, null, GoalTestData.Now)), 9));

        blocked.Attempts[0].Status.ShouldBe(GoalAttemptStatus.Blocked);
        blocked.SettledSequence.ShouldBeNull();
    }

    [Fact]
    public void ApplyTransition_WhenRetryingAFailedGoal_ClearsTheSettlementSequenceAndKeepsTheFailedAttempt()
    {
        var active = Active(out var attempt);
        var failed = Applied(GoalRecordReducer.ApplyTransition(
            active, Request(active, GoalStatus.Failed, "f", new GoalAttemptSettlement(attempt.Id, GoalAttemptStatus.Failed, null, GoalTestData.Now)), 12));

        var retried = Applied(GoalRecordReducer.ApplyTransition(failed, Request(failed, GoalStatus.Ready, "retry"), 13));

        failed.SettledSequence.ShouldBe(12);
        retried.SettledSequence.ShouldBeNull();
        retried.Attempts.Length.ShouldBe(1);
        retried.Goal.ActiveAttemptId.ShouldBeNull();
    }

    [Fact]
    public void ApplyTransition_WhenCancellingAGoalThatNeverStarted_TakesNoAttemptChange()
    {
        var created = Created();

        var cancelled = Applied(GoalRecordReducer.ApplyTransition(created, Request(created, GoalStatus.Cancelled, "x"), 3));

        cancelled.SettledSequence.ShouldBe(3);
        GoalRecordReducer.ApplyTransition(
            created, Request(created, GoalStatus.Cancelled, "y", new GoalAttemptSettlement(new GoalAttemptId(Guid.NewGuid()), GoalAttemptStatus.Cancelled, null, GoalTestData.Now)), 3)
            .Failure!.Kind.ShouldBe(GoalStoreFailureKind.InvalidTransition);
    }
}
