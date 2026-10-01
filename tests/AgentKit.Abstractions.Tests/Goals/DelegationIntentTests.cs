// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

using AgentKit.TestSupport;

/// <summary>Verifies DelegationIntent constraints.</summary>
public sealed class DelegationIntentTests
{
    private static (DelegationRequest Request, AgentGoal Parent, AgentId Agent, SessionId Session, RunId Run) Setup()
    {
        var agent = GoalTestData.NewAgent();
        var session = GoalTestData.NewSession();
        var run = GoalTestData.NewRun();
        var parent = GoalTestData.Goal(agent, session, run);
        var request = GoalTestData.Delegation(parent, new GoalAttemptId(Guid.NewGuid()), run, GoalTestData.NewAgent(), "k", GoalTestData.Authorization(agent, session, run));
        return (request, parent, agent, session, run);
    }

    [Fact]
    public void Constructor_WhenChildBelongsToTheDelegation_RetainsBoth()
    {
        var (request, parent, agent, session, run) = Setup();
        var child = new GoalRecord(GoalTestData.Goal(agent, session, run, parentId: parent.Id, status: GoalStatus.Ready), [], [], request, 1, 1, null);

        var intent = new DelegationIntent(request, child);

        intent.Request.ShouldBeSameAs(request);
        intent.Child.ShouldBeSameAs(child);
    }

    [Fact]
    public void Constructor_WhenAnArgumentIsNull_ThrowsArgumentNullException()
    {
        var (request, parent, agent, session, run) = Setup();
        var child = new GoalRecord(GoalTestData.Goal(agent, session, run, parentId: parent.Id, status: GoalStatus.Ready), [], [], request, 1, 1, null);

        Should.Throw<ArgumentNullException>(() => new DelegationIntent(null!, child)).ParamName.ShouldBe("request");
        Should.Throw<ArgumentNullException>(() => new DelegationIntent(request, null!)).ParamName.ShouldBe("child");
    }

    [Fact]
    public void Constructor_WhenChildHasAnotherParent_ThrowsArgumentException()
    {
        var (request, _, agent, session, run) = Setup();
        var child = new GoalRecord(GoalTestData.Goal(agent, session, run, parentId: new GoalId(Guid.NewGuid()), status: GoalStatus.Ready), [], [], request, 1, 1, null);

        Should.Throw<ArgumentException>(() => new DelegationIntent(request, child)).ParamName.ShouldBe("child");
    }
}
