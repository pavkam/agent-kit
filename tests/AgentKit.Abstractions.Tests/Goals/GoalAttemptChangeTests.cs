// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

using AgentKit.TestSupport;

/// <summary>Verifies GoalAttemptStart and GoalAttemptSettlement constraints.</summary>
public sealed class GoalAttemptChangeTests
{
    [Fact]
    public void GoalAttemptStart_WhenTheAttemptIsNotRunning_ThrowsArgumentException()
    {
        var settled = new GoalAttempt(
            new GoalAttemptId(Guid.NewGuid()), new GoalId(Guid.NewGuid()), GoalTestData.NewAgent(), GoalTestData.NewSession(), GoalTestData.NewRun(), 1,
            GoalAttemptStatus.Failed, new GoalBudgetReservation(new GoalBudget(1, 1, 0)), null, GoalTestData.Now, GoalTestData.Now);

        Should.Throw<ArgumentException>(() => new GoalAttemptStart(settled)).ParamName.ShouldBe("attempt");
    }

    [Fact]
    public void GoalAttemptStart_WhenTheAttemptIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new GoalAttemptStart(null!)).ParamName.ShouldBe("attempt");

    [Fact]
    public void GoalAttemptSettlement_WhenStatusIsRunningOrUndefined_ThrowsArgumentOutOfRangeException()
    {
        var id = new GoalAttemptId(Guid.NewGuid());

        Should.Throw<ArgumentOutOfRangeException>(() => new GoalAttemptSettlement(id, GoalAttemptStatus.Running, null, GoalTestData.Now)).ParamName.ShouldBe("status");
        Should.Throw<ArgumentOutOfRangeException>(() => new GoalAttemptSettlement(id, (GoalAttemptStatus) 99, null, GoalTestData.Now)).ParamName.ShouldBe("status");
    }

    [Fact]
    public void GoalAttemptSettlement_WhenTheAttemptIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new GoalAttemptSettlement(default, GoalAttemptStatus.Failed, null, GoalTestData.Now)).ParamName.ShouldBe("attemptId");
}
