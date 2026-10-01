// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

using AgentKit.TestSupport;

/// <summary>Verifies DelegationChildRunRequest constraints.</summary>
public sealed class DelegationChildRunRequestTests
{
    private static DelegationRequest Delegation()
    {
        var agent = GoalTestData.NewAgent();
        var session = GoalTestData.NewSession();
        var run = GoalTestData.NewRun();
        var parent = GoalTestData.Goal(agent, session, run);
        return GoalTestData.Delegation(parent, new GoalAttemptId(Guid.NewGuid()), run, GoalTestData.NewAgent(), "k", GoalTestData.Authorization(agent, session, run));
    }

    [Fact]
    public void Constructor_WhenValid_RetainsEveryFact()
    {
        var delegation = Delegation();
        var goal = new GoalId(Guid.NewGuid());
        var session = GoalTestData.NewSession();
        var attempt = new GoalAttemptId(Guid.NewGuid());

        var request = new DelegationChildRunRequest(delegation, goal, session, attempt);

        request.Delegation.ShouldBeSameAs(delegation);
        request.ChildGoalId.ShouldBe(goal);
        request.SessionId.ShouldBe(session);
        request.AttemptId.ShouldBe(attempt);
    }

    [Fact]
    public void Constructor_WhenDelegationIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new DelegationChildRunRequest(null!, new GoalId(Guid.NewGuid()), GoalTestData.NewSession(), new GoalAttemptId(Guid.NewGuid()))).ParamName.ShouldBe("delegation");

    [Fact]
    public void Constructor_WhenAnIdentityIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var delegation = Delegation();

        Should.Throw<ArgumentOutOfRangeException>(() => new DelegationChildRunRequest(delegation, default, GoalTestData.NewSession(), new GoalAttemptId(Guid.NewGuid()))).ParamName.ShouldBe("childGoalId");
        Should.Throw<ArgumentOutOfRangeException>(() => new DelegationChildRunRequest(delegation, new GoalId(Guid.NewGuid()), default, new GoalAttemptId(Guid.NewGuid()))).ParamName.ShouldBe("sessionId");
        Should.Throw<ArgumentOutOfRangeException>(() => new DelegationChildRunRequest(delegation, new GoalId(Guid.NewGuid()), GoalTestData.NewSession(), default)).ParamName.ShouldBe("attemptId");
    }
}
