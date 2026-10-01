// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

public sealed class JoinStrategyTests
{
    private static readonly AgentId _agent = GoalTestData.NewAgent();
    private static readonly SessionId _session = GoalTestData.NewSession();
    private static readonly RunId _run = GoalTestData.NewRun();

    public static TheoryData<string> StrategyKeys =>
    [
        GoalJoinStrategyKeys.All.Value,
        GoalJoinStrategyKeys.OrdinalFirstSuccess.Value,
        GoalJoinStrategyKeys.FastestValidSuccess.Value,
        GoalJoinStrategyKeys.Quorum.Value,
        GoalJoinStrategyKeys.BestEffort.Value,
    ];

    private static IGoalJoinStrategy Strategy(GoalJoinStrategyKey key) =>
        key == GoalJoinStrategyKeys.All ? new AllResultsJoinStrategy()
        : key == GoalJoinStrategyKeys.OrdinalFirstSuccess ? new OrdinalFirstSuccessJoinStrategy()
        : key == GoalJoinStrategyKeys.FastestValidSuccess ? new FastestValidSuccessJoinStrategy()
        : key == GoalJoinStrategyKeys.Quorum ? new QuorumJoinStrategy()
        : new BestEffortJoinStrategy();

    private static GoalJoinChild Child(int ordinal, GoalStatus status, bool eligible = false, long? settled = null) =>
        new(ordinal + 1, new GoalId(new Guid(ordinal + 1, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0])), status, eligible, eligible ? GoalTestData.Outcome(_run) : null, settled);

    private static GoalJoinEvaluationRequest Evaluation(GoalJoinStrategyKey key, ImmutableArray<GoalJoinChild> children, int? quorum = null, bool elapsed = false) =>
        new(new GoalJoinRequest(GoalTestData.Profile, new GoalId(Guid.NewGuid()), _agent, _session, _run, GoalTestData.Authorization(_agent, _session, _run), key, quorum, null), children, elapsed);

    private static async Task<GoalJoinDecision> Decide(GoalJoinStrategyKey key, ImmutableArray<GoalJoinChild> children, int? quorum = null, bool elapsed = false) =>
        await Strategy(key).EvaluateAsync(Evaluation(key, children, quorum, elapsed), TestContext.Current.CancellationToken);

    [Theory]
    [MemberData(nameof(StrategyKeys))]
    public void Key_WhenQueried_MatchesItsPublishedKey(string key) => Strategy(new GoalJoinStrategyKey(key)).Key.Value.ShouldBe(key);

    [Theory]
    [MemberData(nameof(StrategyKeys))]
    public async Task EvaluateAsync_WhenRequestIsNull_ThrowsArgumentNullException(string key) =>
        (await Should.ThrowAsync<ArgumentNullException>(async () => await Strategy(new GoalJoinStrategyKey(key)).EvaluateAsync(null!, TestContext.Current.CancellationToken))).ParamName.ShouldBe("request");

    [Theory]
    [MemberData(nameof(StrategyKeys))]
    public async Task EvaluateAsync_WhenCancelled_ThrowsOperationCanceledException(string key)
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();
        var strategy = Strategy(new GoalJoinStrategyKey(key));

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await strategy.EvaluateAsync(Evaluation(new GoalJoinStrategyKey(key), [Child(0, GoalStatus.Active)], 1), cancel.Token));
    }

    [Fact]
    public async Task All_WhenAnyChildIsOpen_IsPendingOnOpenChildrenInOrdinalOrder()
    {
        var children = ImmutableArray.Create(Child(0, GoalStatus.Completed, true, 4), Child(1, GoalStatus.Active), Child(2, GoalStatus.Ready));

        var decision = await Decide(GoalJoinStrategyKeys.All, children);

        decision.ShouldBeOfType<GoalJoinPending>().Awaiting.ShouldBe([children[1].ChildGoalId, children[2].ChildGoalId]);
    }

    [Fact]
    public async Task All_WhenEveryChildIsSettled_IsSatisfiedWithAllChildrenIncludingFailures()
    {
        var children = ImmutableArray.Create(Child(0, GoalStatus.Completed, true, 4), Child(1, GoalStatus.Failed, false, 9));

        var satisfied = (await Decide(GoalJoinStrategyKeys.All, children)).ShouldBeOfType<GoalJoinSatisfied>();

        satisfied.Results.Length.ShouldBe(2);
        satisfied.Winner.ShouldBeNull();
        satisfied.CutoffSequence.ShouldBe(9);
    }

    [Fact]
    public async Task OrdinalFirstSuccess_WhenLaterChildFinishesFirst_WaitsForEarlierOrdinalInsteadOfCompletionOrder()
    {
        var children = ImmutableArray.Create(Child(0, GoalStatus.Active), Child(1, GoalStatus.Completed, true, 2));

        var decision = await Decide(GoalJoinStrategyKeys.OrdinalFirstSuccess, children);

        decision.ShouldBeOfType<GoalJoinPending>().Awaiting.ShouldBe([children[0].ChildGoalId]);
    }

    [Fact]
    public async Task OrdinalFirstSuccess_WhenEarlierChildFailed_SelectsLowestEligibleOrdinal()
    {
        var children = ImmutableArray.Create(Child(0, GoalStatus.Failed, false, 1), Child(1, GoalStatus.Completed, true, 7), Child(2, GoalStatus.Completed, true, 3));

        var satisfied = (await Decide(GoalJoinStrategyKeys.OrdinalFirstSuccess, children)).ShouldBeOfType<GoalJoinSatisfied>();

        satisfied.Winner.ShouldBe(children[1].ChildGoalId);
    }

    [Fact]
    public async Task OrdinalFirstSuccess_WhenNoChildIsEligible_IsUnsatisfiable()
    {
        var children = ImmutableArray.Create(Child(0, GoalStatus.Failed, false, 1), Child(1, GoalStatus.Cancelled, false, 2));

        _ = (await Decide(GoalJoinStrategyKeys.OrdinalFirstSuccess, children)).ShouldBeOfType<GoalJoinUnsatisfiable>();
    }

    [Fact]
    public async Task FastestValidSuccess_WhenSeveralChildrenAreEligible_PicksLowestSettlementSequenceNotOrdinal()
    {
        var children = ImmutableArray.Create(Child(0, GoalStatus.Completed, true, 12), Child(1, GoalStatus.Completed, true, 5), Child(2, GoalStatus.Active));

        var satisfied = (await Decide(GoalJoinStrategyKeys.FastestValidSuccess, children)).ShouldBeOfType<GoalJoinSatisfied>();

        satisfied.Winner.ShouldBe(children[1].ChildGoalId);
        satisfied.CutoffSequence.ShouldBe(5);
    }

    [Fact]
    public async Task FastestValidSuccess_WhenReplayedOverSameDurableState_ReproducesTheSameWinner()
    {
        var children = ImmutableArray.Create(Child(0, GoalStatus.Completed, true, 12), Child(1, GoalStatus.Completed, true, 5));

        var first = await Decide(GoalJoinStrategyKeys.FastestValidSuccess, children);
        var replay = await Decide(GoalJoinStrategyKeys.FastestValidSuccess, [.. children]);

        replay.ShouldBe(first);
    }

    [Fact]
    public async Task FastestValidSuccess_WhenOnlyOpenChildrenRemain_IsPendingAndWhenNoneRemainUnsatisfiable()
    {
        _ = (await Decide(GoalJoinStrategyKeys.FastestValidSuccess, [Child(0, GoalStatus.Ready)])).ShouldBeOfType<GoalJoinPending>();
        _ = (await Decide(GoalJoinStrategyKeys.FastestValidSuccess, [Child(0, GoalStatus.Failed, false, 1)])).ShouldBeOfType<GoalJoinUnsatisfiable>();
    }

    [Fact]
    public async Task Quorum_WhenEnoughChildrenAreEligible_IsSatisfiedWithEligibleChildrenInOrdinalOrder()
    {
        var children = ImmutableArray.Create(Child(0, GoalStatus.Completed, true, 3), Child(1, GoalStatus.Failed, false, 4), Child(2, GoalStatus.Completed, true, 6), Child(3, GoalStatus.Active));

        var satisfied = (await Decide(GoalJoinStrategyKeys.Quorum, children, quorum: 2)).ShouldBeOfType<GoalJoinSatisfied>();

        satisfied.Results.Select(static child => child.Ordinal).ShouldBe([1, 3]);
        satisfied.CutoffSequence.ShouldBe(6);
    }

    [Fact]
    public async Task Quorum_WhenTooFewCanStillSucceed_IsUnsatisfiable()
    {
        var children = ImmutableArray.Create(Child(0, GoalStatus.Completed, true, 3), Child(1, GoalStatus.Failed, false, 4), Child(2, GoalStatus.Failed, false, 5));

        _ = (await Decide(GoalJoinStrategyKeys.Quorum, children, quorum: 2)).ShouldBeOfType<GoalJoinUnsatisfiable>();
    }

    [Fact]
    public async Task Quorum_WhenMissingQuorumSize_IsUnsatisfiable() =>
        _ = (await Decide(GoalJoinStrategyKeys.Quorum, [Child(0, GoalStatus.Active)])).ShouldBeOfType<GoalJoinUnsatisfiable>();

    [Fact]
    public async Task Quorum_WhenQuorumPendingThenDeadlineElapses_BecomesUnsatisfiable()
    {
        var children = ImmutableArray.Create(Child(0, GoalStatus.Completed, true, 3), Child(1, GoalStatus.Active));

        _ = (await Decide(GoalJoinStrategyKeys.Quorum, children, quorum: 2)).ShouldBeOfType<GoalJoinPending>();
        _ = (await Decide(GoalJoinStrategyKeys.Quorum, children, quorum: 2, elapsed: true)).ShouldBeOfType<GoalJoinUnsatisfiable>();
    }

    [Fact]
    public async Task BestEffort_WhenChildrenAreOpenBeforeDeadline_IsPending() =>
        (await Decide(GoalJoinStrategyKeys.BestEffort, [Child(0, GoalStatus.Active)])).ShouldBeOfType<GoalJoinPending>();

    [Fact]
    public async Task BestEffort_WhenDeadlineElapsed_IsSatisfiedWithWhateverSucceeded()
    {
        var children = ImmutableArray.Create(Child(0, GoalStatus.Completed, true, 3), Child(1, GoalStatus.Active));

        var satisfied = (await Decide(GoalJoinStrategyKeys.BestEffort, children, elapsed: true)).ShouldBeOfType<GoalJoinSatisfied>();

        satisfied.Results.Length.ShouldBe(1);
    }

    [Fact]
    public async Task BestEffort_WhenNothingSucceeded_IsSatisfiedWithAnEmptySet()
    {
        var satisfied = (await Decide(GoalJoinStrategyKeys.BestEffort, [Child(0, GoalStatus.Failed, false, 1)])).ShouldBeOfType<GoalJoinSatisfied>();

        satisfied.Results.ShouldBeEmpty();
        satisfied.CutoffSequence.ShouldBe(0);
    }

    [Fact]
    public async Task JoinAsync_WhenChildrenSettleOutOfOrder_OrdinalFirstSuccessStillSelectsEarliestOrdinal()
    {
        var target = GoalTestData.NewAgent();
        await using var harness = new DelegationHarness(targetAgents: [target]);
        var run = DelegationHarness.NewRun();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var first = harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, target, "a"), hooks: null, cancel.Token);
        var childA = await harness.WaitForChildAsync(run);
        var second = harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, target, "b"), hooks: null, cancel.Token);
        var childB = await harness.WaitForChildAsync(run, 2);
        _ = await harness.CompleteChildAsync(childB, "B");
        _ = await harness.CompleteChildAsync(childA, "A");
        harness.Time.Advance(TimeSpan.FromSeconds(1));
        _ = await first;
        _ = await second;
        var join = new GoalJoinRequest(
            GoalTestData.Profile, RunRootGoal.GoalIdFor(run.RunId), run.AgentId, run.SessionId, run.RunId, run.Authorization,
            GoalJoinStrategyKeys.OrdinalFirstSuccess, null, null);

        var decision = await harness.Delegation.JoinAsync(join, hooks: null, TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<GoalJoinSatisfied>().Winner.ShouldBe(childA.Goal.Id);
    }

    [Fact]
    public async Task JoinAsync_WhenStrategyIsNotRegistered_IsUnsatisfiable()
    {
        await using var harness = new DelegationHarness();
        var run = DelegationHarness.NewRun();
        var join = new GoalJoinRequest(
            GoalTestData.Profile, RunRootGoal.GoalIdFor(run.RunId), run.AgentId, run.SessionId, run.RunId, run.Authorization, new GoalJoinStrategyKey("nope"), null, null);

        var decision = await harness.Delegation.JoinAsync(join, hooks: null, TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<GoalJoinUnsatisfiable>();
    }
}
