// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

using AgentKit.TestSupport;

/// <summary>Verifies GoalAttempt constraints.</summary>
public sealed class GoalAttemptTests
{
    private static GoalAttempt Make(
        int number = 1,
        GoalAttemptStatus status = GoalAttemptStatus.Running,
        GoalOutcomeReference? outcome = null,
        DateTimeOffset? ended = null,
        DateTimeOffset? started = null) => new(
            new GoalAttemptId(Guid.NewGuid()), new GoalId(Guid.NewGuid()), GoalTestData.NewAgent(), GoalTestData.NewSession(), GoalTestData.NewRun(),
            number, status, new GoalBudgetReservation(new GoalBudget(1, 1, 0)), outcome, started ?? GoalTestData.Now, ended);

    [Fact]
    public void Constructor_WhenNumberIsNotPositive_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Make(0)).ParamName.ShouldBe("number");

    [Fact]
    public void Constructor_WhenRunningWithAnEndInstant_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Make(ended: GoalTestData.Now.AddMinutes(1))).ParamName.ShouldBe("status");

    [Fact]
    public void Constructor_WhenRunningWithAnOutcome_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Make(outcome: GoalTestData.Outcome(GoalTestData.NewRun()))).ParamName.ShouldBe("status");

    [Fact]
    public void Constructor_WhenSettledWithoutAnEndInstant_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Make(status: GoalAttemptStatus.Failed)).ParamName.ShouldBe("endedAt");

    [Fact]
    public void Constructor_WhenEndPrecedesStart_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Make(status: GoalAttemptStatus.Failed, ended: GoalTestData.Now.AddMinutes(-1))).ParamName.ShouldBe("endedAt");

    [Fact]
    public void Constructor_WhenStatusIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Make(status: (GoalAttemptStatus) 99)).ParamName.ShouldBe("status");

    [Fact]
    public void Constructor_WhenSettledWithAnEndInstantAndNoOutcome_IsAccepted() =>
        Make(status: GoalAttemptStatus.Cancelled, ended: GoalTestData.Now.AddMinutes(1)).Outcome.ShouldBeNull();
}
