// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

using AgentKit.TestSupport;

/// <summary>Verifies GoalJoinChild, GoalJoinEvaluationRequest, and join decision constraints.</summary>
public sealed class GoalJoinChildTests
{
    private static GoalJoinChild Child(int ordinal, GoalStatus status = GoalStatus.Completed, bool eligible = true, long? settled = 1) =>
        new(ordinal, new GoalId(Guid.NewGuid()), status, eligible, null, settled);

    [Fact]
    public void Constructor_WhenEligibleChildIsNotCompleted_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Child(1, GoalStatus.Failed)).ParamName.ShouldBe("eligible");

    [Fact]
    public void Constructor_WhenEligibleChildHasNoSettlementSequence_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Child(1, settled: null)).ParamName.ShouldBe("eligible");

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Constructor_WhenOrdinalIsNotPositive_ThrowsArgumentOutOfRangeException(int ordinal) =>
        Should.Throw<ArgumentOutOfRangeException>(() => Child(ordinal)).ParamName.ShouldBe("ordinal");

    [Theory]
    [InlineData(GoalStatus.Proposed, true)]
    [InlineData(GoalStatus.Ready, true)]
    [InlineData(GoalStatus.Active, true)]
    [InlineData(GoalStatus.Waiting, true)]
    [InlineData(GoalStatus.Completed, false)]
    [InlineData(GoalStatus.Failed, false)]
    [InlineData(GoalStatus.Cancelled, false)]
    [InlineData(GoalStatus.Blocked, false)]
    public void IsOpen_WhenStatusIsEnumerated_IsTrueOnlyForStatusesThatCanStillChangeByThemselves(GoalStatus status, bool open) =>
        Child(1, status, eligible: false, settled: null).IsOpen.ShouldBe(open);

    [Fact]
    public void GoalJoinEvaluationRequest_WhenChildrenAreNotInStrictOrdinalOrder_ThrowsArgumentException()
    {
        var join = JoinRequest();

        Should.Throw<ArgumentException>(() => new GoalJoinEvaluationRequest(join, [Child(2), Child(1)], false)).ParamName.ShouldBe("children");
        Should.Throw<ArgumentException>(() => new GoalJoinEvaluationRequest(join, [Child(1), Child(1)], false)).ParamName.ShouldBe("children");
    }

    [Fact]
    public void GoalJoinEvaluationRequest_WhenChildrenAreDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new GoalJoinEvaluationRequest(JoinRequest(), default, false)).ParamName.ShouldBe("children");

    [Fact]
    public void GoalJoinSatisfied_WhenTheWinnerIsNotAmongTheResults_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new GoalJoinSatisfied([Child(1)], new GoalId(Guid.NewGuid()), 1)).ParamName.ShouldBe("winner");

    [Fact]
    public void GoalJoinSatisfied_WhenCutoffIsNegative_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new GoalJoinSatisfied([], null, -1)).ParamName.ShouldBe("cutoffSequence");

    [Fact]
    public void GoalJoinPending_WhenAwaitingMatchesByContent_IsStructural()
    {
        var id = new GoalId(Guid.NewGuid());

        new GoalJoinPending([id]).ShouldBe(new GoalJoinPending([id]));
    }

    [Fact]
    public void GoalJoinUnsatisfiable_WhenMessageIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new GoalJoinUnsatisfiable(" ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void GoalJoinStrategyKeys_WhenRead_AreDistinctAndStable()
    {
        var keys = new[]
        {
            GoalJoinStrategyKeys.All, GoalJoinStrategyKeys.OrdinalFirstSuccess, GoalJoinStrategyKeys.FastestValidSuccess,
            GoalJoinStrategyKeys.Quorum, GoalJoinStrategyKeys.BestEffort,
        };

        keys.Distinct().Count().ShouldBe(5);
        GoalJoinStrategyKeys.All.Value.ShouldBe("agentkit.join.all");
    }

    [Fact]
    public void GoalJoinRequest_WhenQuorumIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        var agent = new AgentId(Guid.NewGuid());
        var session = new SessionId(Guid.NewGuid());
        var run = new RunId(Guid.NewGuid());

        Should.Throw<ArgumentOutOfRangeException>(() => new GoalJoinRequest(
            GoalTestData.Profile, new GoalId(Guid.NewGuid()), agent, session, run, GoalTestData.Authorization(agent, session, run),
            GoalJoinStrategyKeys.Quorum, 0, null)).ParamName.ShouldBe("quorumSize");
    }

    [Fact]
    public void GoalJoinRequest_WhenAuthorizationBelongsToAnotherAgent_ThrowsArgumentException()
    {
        var session = new SessionId(Guid.NewGuid());
        var run = new RunId(Guid.NewGuid());

        Should.Throw<ArgumentException>(() => new GoalJoinRequest(
            GoalTestData.Profile, new GoalId(Guid.NewGuid()), new AgentId(Guid.NewGuid()), session, run,
            GoalTestData.Authorization(new AgentId(Guid.NewGuid()), session, run), GoalJoinStrategyKeys.All, null, null)).ParamName.ShouldBe("authorization");
    }

    private static GoalJoinRequest JoinRequest()
    {
        var agent = new AgentId(Guid.NewGuid());
        var session = new SessionId(Guid.NewGuid());
        var run = new RunId(Guid.NewGuid());
        return new GoalJoinRequest(
            GoalTestData.Profile, new GoalId(Guid.NewGuid()), agent, session, run, GoalTestData.Authorization(agent, session, run),
            GoalJoinStrategyKeys.All, null, null);
    }
}
