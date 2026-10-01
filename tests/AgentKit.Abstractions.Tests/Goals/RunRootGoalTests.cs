// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

/// <summary>Verifies the implicit run-root goal identity derivation.</summary>
public sealed class RunRootGoalTests
{
    [Fact]
    public void GoalIdFor_WhenTheSameRunIsGiven_IsDeterministicAndCarriesTheRunValue()
    {
        var run = new RunId(Guid.NewGuid());

        RunRootGoal.GoalIdFor(run).ShouldBe(RunRootGoal.GoalIdFor(run));
        RunRootGoal.GoalIdFor(run).Value.ShouldBe(run.Value);
        RunRootGoal.AttemptIdFor(run).Value.ShouldBe(run.Value);
    }

    [Fact]
    public void GoalIdFor_WhenRunsDiffer_ReturnsDistinctGoals() =>
        RunRootGoal.GoalIdFor(new RunId(Guid.NewGuid())).ShouldNotBe(RunRootGoal.GoalIdFor(new RunId(Guid.NewGuid())));

    [Fact]
    public void IdentityDerivation_WhenRunIsDefault_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => RunRootGoal.GoalIdFor(default)).ParamName.ShouldBe("runId");
        Should.Throw<ArgumentOutOfRangeException>(() => RunRootGoal.AttemptIdFor(default)).ParamName.ShouldBe("runId");
    }

    [Fact]
    public void IsRootOf_WhenTheGoalCarriesTheRunValue_IsTrueOnlyForThatRun()
    {
        var run = new RunId(Guid.NewGuid());

        RunRootGoal.IsRootOf(RunRootGoal.GoalIdFor(run), run).ShouldBeTrue();
        RunRootGoal.IsRootOf(new GoalId(Guid.NewGuid()), run).ShouldBeFalse();
    }
}
