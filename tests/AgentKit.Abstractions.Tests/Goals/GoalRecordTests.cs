// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

using AgentKit.TestSupport;

/// <summary>Verifies GoalRecord constraints and derived members.</summary>
public sealed class GoalRecordTests
{
    private static readonly AgentId _agent = GoalTestData.NewAgent();
    private static readonly SessionId _session = GoalTestData.NewSession();
    private static readonly RunId _run = GoalTestData.NewRun();

    [Fact]
    public void Constructor_WhenAnAttemptBelongsToAnotherGoal_ThrowsArgumentException()
    {
        var goal = GoalTestData.Goal(_agent, _session, _run);
        var foreign = GoalTestData.Attempt(new GoalId(Guid.NewGuid()), 1, _agent, _session, _run);

        Should.Throw<ArgumentException>(() => new GoalRecord(goal, [foreign], [], null, 1, null, null)).ParamName.ShouldBe("attempts");
    }

    [Fact]
    public void Constructor_WhenASequenceIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        var goal = GoalTestData.Goal(_agent, _session, _run);

        Should.Throw<ArgumentOutOfRangeException>(() => new GoalRecord(goal, [], [], null, 0, null, null)).ParamName.ShouldBe("sequence");
        Should.Throw<ArgumentOutOfRangeException>(() => new GoalRecord(goal, [], [], null, 1, null, 0)).ParamName.ShouldBe("settledSequence");
    }

    [Fact]
    public void Constructor_WhenARootGoalCarriesAChildOrdinal_ThrowsArgumentException()
    {
        var goal = GoalTestData.Goal(_agent, _session, _run);

        Should.Throw<ArgumentException>(() => new GoalRecord(goal, [], [], null, 1, 1, null)).ParamName.ShouldBe("childOrdinal");
    }

    [Fact]
    public void Constructor_WhenArraysAreDefault_ThrowsArgumentException()
    {
        var goal = GoalTestData.Goal(_agent, _session, _run);

        Should.Throw<ArgumentException>(() => new GoalRecord(goal, default, [], null, 1, null, null)).ParamName.ShouldBe("attempts");
        Should.Throw<ArgumentException>(() => new GoalRecord(goal, [], default, null, 1, null, null)).ParamName.ShouldBe("transitions");
    }

    [Fact]
    public void ActiveAttempt_WhenTheGoalNamesARunningAttempt_ReturnsIt()
    {
        var goal = GoalTestData.Goal(_agent, _session, _run);
        var attempt = GoalTestData.Attempt(goal.Id, 1, _agent, _session, _run);
        var active = goal.WithState(GoalStatus.Active, attempt.Id, new VersionToken("3"));

        new GoalRecord(active, [attempt], [], null, 1, null, null).ActiveAttempt.ShouldBe(attempt);
        new GoalRecord(goal, [attempt], [], null, 1, null, null).ActiveAttempt.ShouldBeNull();
    }

    [Fact]
    public void Equality_WhenMembersMatchByContent_IsStructural()
    {
        var goal = GoalTestData.Goal(_agent, _session, _run);

        new GoalRecord(goal, [], [], null, 1, null, null).ShouldBe(new GoalRecord(goal, [], [], null, 1, null, null));
        new GoalRecord(goal, [], [], null, 1, null, null).ShouldNotBe(new GoalRecord(goal, [], [], null, 2, null, null));
    }
}
