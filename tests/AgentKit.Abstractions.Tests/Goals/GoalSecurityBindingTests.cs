// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

using AgentKit.TestSupport;

/// <summary>Verifies GoalSecurityBinding resources and canonical fingerprints.</summary>
public sealed class GoalSecurityBindingTests
{
    private static readonly TestGoalGrants _grants = new();
    private readonly GoalRequestFactory _factory = new(_grants, new ComponentId("store"));

    [Fact]
    public void Resource_WhenGoalIsDefault_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => GoalSecurityBinding.Resource(default)).ParamName.ShouldBe("goalId");
        Should.Throw<ArgumentOutOfRangeException>(() => GoalSecurityBinding.ChildrenResource(default)).ParamName.ShouldBe("parentId");
    }

    [Fact]
    public void Resource_WhenGoalsDiffer_NamesDistinctApplicationStateResources()
    {
        var first = GoalSecurityBinding.Resource(new GoalId(Guid.NewGuid()));
        var second = GoalSecurityBinding.Resource(new GoalId(Guid.NewGuid()));

        first.Kind.ShouldBe(ProtectedResourceKind.ApplicationState);
        first.ShouldNotBe(second);
        GoalSecurityBinding.ChildrenResource(new GoalId(Guid.NewGuid())).Identifier.ShouldStartWith("goal-children:");
    }

    [Fact]
    public void Fingerprint_WhenOnlyTheGrantDiffers_IsIdentical()
    {
        var owner = Owner();
        var goal = GoalTestData.Goal(owner.Agent, owner.Session, owner.Run);

        var first = GoalSecurityBinding.Fingerprint(_factory.Create(goal, owner.Authorization, "k"));
        var second = GoalSecurityBinding.Fingerprint(_factory.Create(goal, owner.Authorization, "k"));

        first.ShouldBe(second);
    }

    [Fact]
    public void Fingerprint_WhenAnyBoundFactChanges_Differs()
    {
        var owner = Owner();
        var goal = GoalTestData.Goal(owner.Agent, owner.Session, owner.Run);
        var baseline = GoalSecurityBinding.Fingerprint(_factory.Create(goal, owner.Authorization, "k"));

        GoalSecurityBinding.Fingerprint(_factory.Create(goal, owner.Authorization, "other-key")).ShouldNotBe(baseline);
        GoalSecurityBinding.Fingerprint(_factory.Create(GoalTestData.Goal(owner.Agent, owner.Session, owner.Run), owner.Authorization, "k")).ShouldNotBe(baseline);
    }

    [Fact]
    public void Fingerprint_WhenTransitionsDifferInTargetOrAttempt_Differs()
    {
        var owner = Owner();
        var goal = GoalTestData.Goal(owner.Agent, owner.Session, owner.Run);
        var record = new GoalRecord(goal, [], [], null, 1, null, null);
        var ready = _factory.Transition(GoalTestData.Transition(record, GoalStatus.Ready, "k"), null, owner.Authorization);
        var cancelled = _factory.Transition(GoalTestData.Transition(record, GoalStatus.Cancelled, "k"), null, owner.Authorization);

        GoalSecurityBinding.Fingerprint(ready).ShouldNotBe(GoalSecurityBinding.Fingerprint(cancelled));
    }

    [Fact]
    public void Fingerprint_WhenLoadAndChildrenRequestsDiffer_Differs()
    {
        var owner = Owner();
        var id = new GoalId(Guid.NewGuid());

        GoalSecurityBinding.Fingerprint(_factory.Children(id, 0, 5, owner.Authorization))
            .ShouldNotBe(GoalSecurityBinding.Fingerprint(_factory.Children(id, 1, 5, owner.Authorization)));
        GoalSecurityBinding.Fingerprint(_factory.Load(id, owner.Authorization))
            .ShouldNotBe(GoalSecurityBinding.Fingerprint(_factory.Load(new GoalId(Guid.NewGuid()), owner.Authorization)));
    }

    [Fact]
    public void Fingerprint_WhenARequestIsNull_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => GoalSecurityBinding.Fingerprint((GoalCreateRequest) null!)).ParamName.ShouldBe("request");
        Should.Throw<ArgumentNullException>(() => GoalSecurityBinding.Fingerprint((GoalLoadRequest) null!)).ParamName.ShouldBe("request");
        Should.Throw<ArgumentNullException>(() => GoalSecurityBinding.Fingerprint((GoalTransitionRequest) null!)).ParamName.ShouldBe("request");
        Should.Throw<ArgumentNullException>(() => GoalSecurityBinding.Fingerprint((GoalChildrenRequest) null!)).ParamName.ShouldBe("request");
    }

    [Fact]
    public void DelegationFingerprint_WhenTheRequestChanges_Differs()
    {
        var owner = Owner();
        var parent = GoalTestData.Goal(owner.Agent, owner.Session, owner.Run);
        var attempt = new GoalAttemptId(Guid.NewGuid());
        var target = GoalTestData.NewAgent();
        var first = GoalTestData.Delegation(parent, attempt, owner.Run, target, "k", owner.Authorization);
        var second = GoalTestData.Delegation(parent, attempt, owner.Run, target, "k", owner.Authorization);

        DelegationSecurityBinding.Fingerprint(first).ShouldNotBe(DelegationSecurityBinding.Fingerprint(second));
        DelegationSecurityBinding.Fingerprint(first).ShouldBe(DelegationSecurityBinding.Fingerprint(first));
        DelegationSecurityBinding.Resource(first.Id).Kind.ShouldBe(ProtectedResourceKind.Delegation);
    }

    [Fact]
    public void DelegationBinding_WhenArgumentsAreInvalid_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => DelegationSecurityBinding.Resource(default)).ParamName.ShouldBe("id");
        Should.Throw<ArgumentNullException>(() => DelegationSecurityBinding.Fingerprint(null!)).ParamName.ShouldBe("request");
    }

    private static (AgentId Agent, SessionId Session, RunId Run, SecurityAuthorizationContext Authorization) Owner()
    {
        var agent = GoalTestData.NewAgent();
        var session = GoalTestData.NewSession();
        var run = GoalTestData.NewRun();
        return (agent, session, run, GoalTestData.Authorization(agent, session, run));
    }
}
