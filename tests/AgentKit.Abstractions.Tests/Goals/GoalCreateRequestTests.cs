// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

using AgentKit.TestSupport;

/// <summary>Verifies GoalCreateRequest constraints.</summary>
public sealed class GoalCreateRequestTests
{
    private static readonly TestGoalGrants _grants = new();

    private static SecurityGrant Grant(AgentGoal goal) =>
        _grants.Issue(new ComponentId("store"), GoalTestData.Authorization(goal.OwnerAgentId, goal.SessionId, goal.OriginatingRunId),
            SecurityOperationKind.StateMutation, SecurityEffect.Create, [GoalSecurityBinding.Resource(goal.Id)], new InputFingerprint("f"));

    [Fact]
    public void Constructor_WhenTheGoalIsProposedOrReady_Succeeds()
    {
        var goal = GoalTestData.Goal(GoalTestData.NewAgent(), GoalTestData.NewSession(), GoalTestData.NewRun(), status: GoalStatus.Ready);

        new GoalCreateRequest(goal, null, new IdempotencyKey("k"), Grant(goal)).Profile.ShouldBe(GoalTestData.Profile);
    }

    [Fact]
    public void Constructor_WhenTheGoalIsAlreadyCompleted_ThrowsArgumentException()
    {
        var goal = GoalTestData.Goal(GoalTestData.NewAgent(), GoalTestData.NewSession(), GoalTestData.NewRun())
            .WithState(GoalStatus.Completed, null, new VersionToken("1"));

        Should.Throw<ArgumentException>(() => new GoalCreateRequest(goal, null, new IdempotencyKey("k"), Grant(goal))).ParamName.ShouldBe("goal");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreNull_ThrowsArgumentNullException()
    {
        var goal = GoalTestData.Goal(GoalTestData.NewAgent(), GoalTestData.NewSession(), GoalTestData.NewRun());

        Should.Throw<ArgumentNullException>(() => new GoalCreateRequest(null!, null, new IdempotencyKey("k"), Grant(goal))).ParamName.ShouldBe("goal");
        Should.Throw<ArgumentNullException>(() => new GoalCreateRequest(goal, null, new IdempotencyKey("k"), null!)).ParamName.ShouldBe("grant");
    }

    [Fact]
    public void Constructor_WhenTheKeyIsDefault_ThrowsArgumentException()
    {
        var goal = GoalTestData.Goal(GoalTestData.NewAgent(), GoalTestData.NewSession(), GoalTestData.NewRun());

        Should.Throw<ArgumentException>(() => new GoalCreateRequest(goal, null, default, Grant(goal))).ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void Constructor_WhenTheDelegationNamesADifferentParent_ThrowsArgumentException()
    {
        var agent = GoalTestData.NewAgent();
        var session = GoalTestData.NewSession();
        var run = GoalTestData.NewRun();
        var parent = GoalTestData.Goal(agent, session, run);
        var otherParent = GoalTestData.Goal(agent, session, run);
        var child = GoalTestData.Goal(agent, session, run, parent.Id);
        var delegation = GoalTestData.Delegation(otherParent, new GoalAttemptId(Guid.NewGuid()), run, GoalTestData.NewAgent(), "d", GoalTestData.Authorization(agent, session, run));

        Should.Throw<ArgumentException>(() => new GoalCreateRequest(child, delegation, new IdempotencyKey("k"), Grant(child))).ParamName.ShouldBe("delegation");
    }
}
