// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

using AgentKit.TestSupport;

/// <summary>Verifies GoalTransitionRequest, GoalLoadRequest, GoalChildrenRequest, and GoalAttemptRequest constraints.</summary>
public sealed class GoalTransitionRequestTests
{
    private static readonly TestGoalGrants _grants = new();

    private static SecurityGrant Grant()
    {
        var authorization = GoalTestData.Authorization(GoalTestData.NewAgent(), GoalTestData.NewSession(), GoalTestData.NewRun());
        return _grants.Issue(new ComponentId("store"), authorization, SecurityOperationKind.StateRead, SecurityEffect.Observe,
            [new ProtectedResource(ProtectedResourceKind.ApplicationState, "r")], new InputFingerprint("f"));
    }

    private static GoalTransition Transition() => new(
        new GoalId(Guid.NewGuid()), GoalTestData.NewAgent(), GoalTestData.NewSession(), GoalTestData.NewRun(), GoalTestData.NewOperation(),
        GoalStatus.Proposed, GoalStatus.Ready, TransitionActor.Agent, GoalTransitionReason.Admitted, new VersionToken("1"), new IdempotencyKey("k"), GoalTestData.Now);

    [Fact]
    public void GoalTransitionRequest_WhenRequiredArgumentsAreNull_ThrowsWithTheExactParameterName()
    {
        Should.Throw<ArgumentNullException>(() => new GoalTransitionRequest(null!, Transition(), null, Grant())).ParamName.ShouldBe("profile");
        Should.Throw<ArgumentNullException>(() => new GoalTransitionRequest(GoalTestData.Profile, null!, null, Grant())).ParamName.ShouldBe("transition");
        Should.Throw<ArgumentNullException>(() => new GoalTransitionRequest(GoalTestData.Profile, Transition(), null, null!)).ParamName.ShouldBe("grant");
    }

    [Fact]
    public void GoalLoadRequest_WhenGoalIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new GoalLoadRequest(GoalTestData.Profile, default, Grant())).ParamName.ShouldBe("goalId");

    [Theory]
    [InlineData(-1, 1, "afterOrdinal")]
    [InlineData(0, 0, "limit")]
    [InlineData(0, -5, "limit")]
    public void GoalChildrenRequest_WhenCursorOrLimitIsInvalid_ThrowsArgumentOutOfRangeExceptionNamingIt(long after, int limit, string parameter) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new GoalChildrenRequest(GoalTestData.Profile, new GoalId(Guid.NewGuid()), after, limit, Grant())).ParamName.ShouldBe(parameter);

    [Fact]
    public void GoalChildrenRequest_WhenParentIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new GoalChildrenRequest(GoalTestData.Profile, default, 0, 1, Grant())).ParamName.ShouldBe("parentId");

    [Fact]
    public void GoalIntentScanRequest_WhenArgumentsAreInvalid_ThrowsWithTheExactParameterName()
    {
        Should.Throw<ArgumentException>(() => new GoalIntentScanRequest(default, 0, 1)).ParamName.ShouldBe("scanner");
        Should.Throw<ArgumentOutOfRangeException>(() => new GoalIntentScanRequest(new ComponentId("w"), -1, 1)).ParamName.ShouldBe("afterSequence");
        Should.Throw<ArgumentOutOfRangeException>(() => new GoalIntentScanRequest(new ComponentId("w"), 0, 0)).ParamName.ShouldBe("limit");
    }
}
