// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

using AgentKit.TestSupport;

/// <summary>Verifies the coordinator-level goal commands that carry captured authorization.</summary>
public sealed class GoalCommandTests
{
    private readonly AgentId _agent = GoalTestData.NewAgent();
    private readonly SessionId _session = GoalTestData.NewSession();
    private readonly RunId _run = GoalTestData.NewRun();

    private SecurityAuthorizationContext Authorization() => GoalTestData.Authorization(_agent, _session, _run);

    [Fact]
    public void GoalCreateCommand_WhenTheAuthorizationOwnsTheGoal_ExposesTheImpliedProfile()
    {
        var goal = GoalTestData.Goal(_agent, _session, _run);

        new GoalCreateCommand(goal, null, new IdempotencyKey("k"), Authorization()).Profile.ShouldBe(GoalTestData.Profile);
    }

    [Fact]
    public void GoalCreateCommand_WhenTheAuthorizationNamesAnotherAgentOrSession_ThrowsArgumentException()
    {
        var goal = GoalTestData.Goal(_agent, _session, _run);

        Should.Throw<ArgumentException>(() => new GoalCreateCommand(goal, null, new IdempotencyKey("k"), GoalTestData.Authorization(GoalTestData.NewAgent(), _session, _run)))
            .ParamName.ShouldBe("authorization");
        Should.Throw<ArgumentException>(() => new GoalCreateCommand(goal, null, new IdempotencyKey("k"), GoalTestData.Authorization(_agent, GoalTestData.NewSession(), _run)))
            .ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void GoalCreateCommand_WhenTheGoalIsCompletedOrTheKeyIsBlank_Throws()
    {
        var done = GoalTestData.Goal(_agent, _session, _run).WithState(GoalStatus.Completed, null, new VersionToken("1"));

        Should.Throw<ArgumentException>(() => new GoalCreateCommand(done, null, new IdempotencyKey("k"), Authorization())).ParamName.ShouldBe("goal");
        Should.Throw<ArgumentException>(() => new GoalCreateCommand(GoalTestData.Goal(_agent, _session, _run), null, default, Authorization())).ParamName.ShouldBe("idempotencyKey");
        Should.Throw<ArgumentNullException>(() => new GoalCreateCommand(null!, null, new IdempotencyKey("k"), Authorization())).ParamName.ShouldBe("goal");
        Should.Throw<ArgumentNullException>(() => new GoalCreateCommand(GoalTestData.Goal(_agent, _session, _run), null, new IdempotencyKey("k"), null!)).ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void GoalCreateCommand_WhenTheDelegationNamesADifferentParent_ThrowsArgumentException()
    {
        var parent = GoalTestData.Goal(_agent, _session, _run);
        var other = GoalTestData.Goal(_agent, _session, _run);
        var child = GoalTestData.Goal(_agent, _session, _run, parent.Id);
        var delegation = GoalTestData.Delegation(other, new GoalAttemptId(Guid.NewGuid()), _run, GoalTestData.NewAgent(), "d", Authorization());

        Should.Throw<ArgumentException>(() => new GoalCreateCommand(child, delegation, new IdempotencyKey("k"), Authorization())).ParamName.ShouldBe("delegation");
    }

    [Fact]
    public void GoalTransitionCommand_WhenTheAuthorizationDoesNotOwnTheTransition_ThrowsArgumentException()
    {
        var record = new GoalRecord(GoalTestData.Goal(_agent, _session, _run), [], [], null, 1, null, null);
        var transition = GoalTestData.Transition(record, GoalStatus.Ready, "k");

        _ = new GoalTransitionCommand(GoalTestData.Profile, transition, null, Authorization());
        Should.Throw<ArgumentException>(() => new GoalTransitionCommand(GoalTestData.Profile, transition, null, GoalTestData.Authorization(GoalTestData.NewAgent(), _session, _run)))
            .ParamName.ShouldBe("authorization");
        Should.Throw<ArgumentNullException>(() => new GoalTransitionCommand(null!, transition, null, Authorization())).ParamName.ShouldBe("profile");
        Should.Throw<ArgumentNullException>(() => new GoalTransitionCommand(GoalTestData.Profile, null!, null, Authorization())).ParamName.ShouldBe("transition");
    }

    [Fact]
    public void GoalLoadCommand_WhenArgumentsAreInvalid_ThrowsWithTheExactParameterName()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new GoalLoadCommand(GoalTestData.Profile, default, Authorization())).ParamName.ShouldBe("goalId");
        Should.Throw<ArgumentNullException>(() => new GoalLoadCommand(null!, new GoalId(Guid.NewGuid()), Authorization())).ParamName.ShouldBe("profile");
        Should.Throw<ArgumentNullException>(() => new GoalLoadCommand(GoalTestData.Profile, new GoalId(Guid.NewGuid()), null!)).ParamName.ShouldBe("authorization");
    }

    [Theory]
    [InlineData(-1, 1, "afterOrdinal")]
    [InlineData(0, 0, "limit")]
    public void GoalChildrenCommand_WhenCursorOrLimitIsInvalid_ThrowsArgumentOutOfRangeExceptionNamingIt(long after, int limit, string parameter) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new GoalChildrenCommand(GoalTestData.Profile, new GoalId(Guid.NewGuid()), after, limit, Authorization())).ParamName.ShouldBe(parameter);

    [Fact]
    public void GoalChildrenCommand_WhenParentOrProfileIsMissing_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new GoalChildrenCommand(GoalTestData.Profile, default, 0, 1, Authorization())).ParamName.ShouldBe("parentId");
        Should.Throw<ArgumentNullException>(() => new GoalChildrenCommand(null!, new GoalId(Guid.NewGuid()), 0, 1, Authorization())).ParamName.ShouldBe("profile");
    }

    [Fact]
    public void Fingerprints_WhenBuiltFromPartsOrFromAStoreRequest_AgreeExactly()
    {
        var factory = new GoalRequestFactory(new TestGoalGrants(), new ComponentId("store"));
        var goal = GoalTestData.Goal(_agent, _session, _run);
        var record = new GoalRecord(goal, [], [], null, 1, null, null);
        var transition = GoalTestData.Transition(record, GoalStatus.Ready, "k");
        var create = factory.Create(goal, Authorization(), "k");
        var move = factory.Transition(transition, null, Authorization());
        var load = factory.Load(goal.Id, Authorization());
        var children = factory.Children(goal.Id, 2, 5, Authorization());

        GoalSecurityBinding.CreateFingerprint(goal, null, new IdempotencyKey("k")).ShouldBe(GoalSecurityBinding.Fingerprint(create));
        GoalSecurityBinding.TransitionFingerprint(GoalTestData.Profile, transition, null).ShouldBe(GoalSecurityBinding.Fingerprint(move));
        GoalSecurityBinding.LoadFingerprint(GoalTestData.Profile, goal.Id).ShouldBe(GoalSecurityBinding.Fingerprint(load));
        GoalSecurityBinding.ChildrenFingerprint(GoalTestData.Profile, goal.Id, 2, 5).ShouldBe(GoalSecurityBinding.Fingerprint(children));
    }

    [Fact]
    public void PartFingerprints_WhenArgumentsAreInvalid_ThrowWithTheExactParameterName()
    {
        Should.Throw<ArgumentNullException>(() => GoalSecurityBinding.CreateFingerprint(null!, null, new IdempotencyKey("k"))).ParamName.ShouldBe("goal");
        Should.Throw<ArgumentException>(() => GoalSecurityBinding.CreateFingerprint(GoalTestData.Goal(_agent, _session, _run), null, default)).ParamName.ShouldBe("idempotencyKey");
        Should.Throw<ArgumentNullException>(() => GoalSecurityBinding.LoadFingerprint(null!, new GoalId(Guid.NewGuid()))).ParamName.ShouldBe("profile");
        Should.Throw<ArgumentOutOfRangeException>(() => GoalSecurityBinding.LoadFingerprint(GoalTestData.Profile, default)).ParamName.ShouldBe("goalId");
        Should.Throw<ArgumentNullException>(() => GoalSecurityBinding.TransitionFingerprint(GoalTestData.Profile, null!, null)).ParamName.ShouldBe("transition");
        Should.Throw<ArgumentOutOfRangeException>(() => GoalSecurityBinding.ChildrenFingerprint(GoalTestData.Profile, new GoalId(Guid.NewGuid()), -1, 1)).ParamName.ShouldBe("afterOrdinal");
        Should.Throw<ArgumentOutOfRangeException>(() => GoalSecurityBinding.ChildrenFingerprint(GoalTestData.Profile, new GoalId(Guid.NewGuid()), 0, 0)).ParamName.ShouldBe("limit");
    }
}
