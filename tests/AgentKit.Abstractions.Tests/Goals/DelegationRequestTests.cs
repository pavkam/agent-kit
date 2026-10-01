// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

using AgentKit.TestSupport;

/// <summary>Verifies DelegationRequest constraints.</summary>
public sealed class DelegationRequestTests
{
    private readonly AgentId _agent = GoalTestData.NewAgent();
    private readonly SessionId _session = GoalTestData.NewSession();
    private readonly RunId _run = GoalTestData.NewRun();

    [Fact]
    public void Constructor_WhenAuthorizationBelongsToTheParent_RetainsEveryCapturedFact()
    {
        var authorization = GoalTestData.Authorization(_agent, _session, _run);
        var parent = GoalTestData.Goal(_agent, _session, _run);

        var request = GoalTestData.Delegation(parent, new GoalAttemptId(Guid.NewGuid()), _run, GoalTestData.NewAgent(), "k", authorization);

        request.Authorization.ShouldBeSameAs(authorization);
        request.ProfileKey.ShouldBe(GoalTestData.Profile.Key);
        request.JoinStrategyKey.ShouldBe(GoalJoinStrategyKeys.All);
        request.Budget.ScopeId.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenAuthorizationNamesAnotherAgent_ThrowsArgumentException()
    {
        var parent = GoalTestData.Goal(_agent, _session, _run);
        var foreign = GoalTestData.Authorization(GoalTestData.NewAgent(), _session, _run);

        Should.Throw<ArgumentException>(() => GoalTestData.Delegation(parent, new GoalAttemptId(Guid.NewGuid()), _run, GoalTestData.NewAgent(), "k", foreign))
            .ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void Constructor_WhenAuthorizationNamesAnotherSession_ThrowsArgumentException()
    {
        var parent = GoalTestData.Goal(_agent, _session, _run);
        var foreign = GoalTestData.Authorization(_agent, GoalTestData.NewSession(), _run);

        Should.Throw<ArgumentException>(() => GoalTestData.Delegation(parent, new GoalAttemptId(Guid.NewGuid()), _run, GoalTestData.NewAgent(), "k", foreign))
            .ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void Constructor_WhenInRunAuthorizationNamesAnotherRun_ThrowsArgumentException()
    {
        var parent = GoalTestData.Goal(_agent, _session, _run);
        var foreign = GoalTestData.Authorization(_agent, _session, GoalTestData.NewRun());

        Should.Throw<ArgumentException>(() => GoalTestData.Delegation(parent, new GoalAttemptId(Guid.NewGuid()), _run, GoalTestData.NewAgent(), "k", foreign))
            .ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void Constructor_WhenTargetOrParentAttemptIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var parent = GoalTestData.Goal(_agent, _session, _run);
        var authorization = GoalTestData.Authorization(_agent, _session, _run);

        Should.Throw<ArgumentOutOfRangeException>(() => GoalTestData.Delegation(parent, default, _run, GoalTestData.NewAgent(), "k", authorization))
            .ParamName.ShouldBe("parentAttemptId");
        Should.Throw<ArgumentOutOfRangeException>(() => GoalTestData.Delegation(parent, new GoalAttemptId(Guid.NewGuid()), _run, default, "k", authorization))
            .ParamName.ShouldBe("targetAgentId");
    }

    [Fact]
    public void Constructor_WhenIdempotencyKeyIsBlank_ThrowsArgumentException()
    {
        var parent = GoalTestData.Goal(_agent, _session, _run);
        var authorization = GoalTestData.Authorization(_agent, _session, _run);

        Should.Throw<ArgumentException>(() => GoalTestData.Delegation(parent, new GoalAttemptId(Guid.NewGuid()), _run, GoalTestData.NewAgent(), " ", authorization))
            .ParamName.ShouldBe("value");
    }
}
