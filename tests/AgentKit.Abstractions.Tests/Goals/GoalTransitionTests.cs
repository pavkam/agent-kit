// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

using AgentKit.TestSupport;

/// <summary>Verifies the goal transition validity table, constructor constraints, and value semantics.</summary>
public sealed class GoalTransitionTests
{
    private static readonly GoalStatus[] _all = Enum.GetValues<GoalStatus>();

    public static TheoryData<GoalStatus, GoalStatus, bool> Matrix()
    {
        var allowed = new Dictionary<GoalStatus, GoalStatus[]>
        {
            [GoalStatus.Proposed] = [GoalStatus.Ready, GoalStatus.Blocked, GoalStatus.Failed, GoalStatus.Cancelled],
            [GoalStatus.Ready] = [GoalStatus.Active, GoalStatus.Blocked, GoalStatus.Failed, GoalStatus.Cancelled],
            [GoalStatus.Active] = [GoalStatus.Waiting, GoalStatus.Completed, GoalStatus.Failed, GoalStatus.Blocked, GoalStatus.Cancelled],
            [GoalStatus.Waiting] = [GoalStatus.Active, GoalStatus.Completed, GoalStatus.Failed, GoalStatus.Blocked, GoalStatus.Cancelled],
            [GoalStatus.Completed] = [],
            [GoalStatus.Failed] = [GoalStatus.Ready, GoalStatus.Cancelled],
            [GoalStatus.Cancelled] = [],
            [GoalStatus.Blocked] = [GoalStatus.Ready, GoalStatus.Failed, GoalStatus.Cancelled],
        };
        var data = new TheoryData<GoalStatus, GoalStatus, bool>();
        foreach (var from in _all)
        {
            foreach (var to in _all)
            {
                data.Add(from, to, allowed[from].Contains(to));
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void IsValid_WhenPairIsEnumerated_MatchesTheDocumentedMatrix(GoalStatus from, GoalStatus to, bool expected) =>
        GoalTransition.IsValid(from, to).ShouldBe(expected);

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Constructor_WhenPairIsEnumerated_AcceptsExactlyTheValidPairs(GoalStatus from, GoalStatus to, bool expected)
    {
        if (expected)
        {
            Build(from, to).To.ShouldBe(to);
        }
        else
        {
            Should.Throw<ArgumentOutOfRangeException>(() => Build(from, to)).ParamName.ShouldBe("to");
        }
    }

    [Fact]
    public void IsValid_WhenAStatusIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => GoalTransition.IsValid((GoalStatus) 99, GoalStatus.Ready)).ParamName.ShouldBe("from");
        Should.Throw<ArgumentOutOfRangeException>(() => GoalTransition.IsValid(GoalStatus.Ready, (GoalStatus) 99)).ParamName.ShouldBe("to");
    }

    [Fact]
    public void IsTerminal_WhenStatusHasNoTargets_IsTrueForExactlyCompletedAndCancelled() =>
        _all.Where(GoalTransition.IsTerminal).ShouldBe([GoalStatus.Completed, GoalStatus.Cancelled]);

    [Fact]
    public void ValidTargets_WhenStatusIsActive_ListsTheFiveReachableStatuses() =>
        GoalTransition.ValidTargets(GoalStatus.Active).ShouldBe(
            [GoalStatus.Waiting, GoalStatus.Completed, GoalStatus.Failed, GoalStatus.Blocked, GoalStatus.Cancelled]);

    [Fact]
    public void ValidTargets_WhenStatusIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => GoalTransition.ValidTargets((GoalStatus) 99)).ParamName.ShouldBe("from");

    [Fact]
    public void Constructor_WhenAnIdentityIsDefault_ThrowsArgumentOutOfRangeExceptionNamingIt()
    {
        var goal = new GoalId(Guid.NewGuid());
        var agent = GoalTestData.NewAgent();
        var session = GoalTestData.NewSession();
        var run = GoalTestData.NewRun();
        var operation = GoalTestData.NewOperation();

        Should.Throw<ArgumentOutOfRangeException>(() => Identified(default, agent, session, run, operation)).ParamName.ShouldBe("goalId");
        Should.Throw<ArgumentOutOfRangeException>(() => Identified(goal, default, session, run, operation)).ParamName.ShouldBe("ownerAgentId");
        Should.Throw<ArgumentOutOfRangeException>(() => Identified(goal, agent, default, run, operation)).ParamName.ShouldBe("sessionId");
        Should.Throw<ArgumentOutOfRangeException>(() => Identified(goal, agent, session, default, operation)).ParamName.ShouldBe("runId");
        Should.Throw<ArgumentOutOfRangeException>(() => Identified(goal, agent, session, run, default)).ParamName.ShouldBe("operationId");
    }

    [Fact]
    public void Constructor_WhenAnEnumIsUndefinedOrATokenIsBlank_ThrowsWithTheExactParameterName()
    {
        static GoalTransition With(TransitionActor actor, GoalTransitionReason reason, VersionToken version, IdempotencyKey key) => new(
            new GoalId(Guid.NewGuid()), GoalTestData.NewAgent(), GoalTestData.NewSession(), GoalTestData.NewRun(), GoalTestData.NewOperation(),
            GoalStatus.Proposed, GoalStatus.Ready, actor, reason, version, key, GoalTestData.Now);
        var version = new VersionToken("1");
        var key = new IdempotencyKey("k");

        Should.Throw<ArgumentOutOfRangeException>(() => With((TransitionActor) 99, GoalTransitionReason.Admitted, version, key)).ParamName.ShouldBe("actor");
        Should.Throw<ArgumentOutOfRangeException>(() => With(TransitionActor.Agent, (GoalTransitionReason) 99, version, key)).ParamName.ShouldBe("reason");
        Should.Throw<ArgumentException>(() => With(TransitionActor.Agent, GoalTransitionReason.Admitted, default, key)).ParamName.ShouldBe("expectedVersion");
        Should.Throw<ArgumentException>(() => With(TransitionActor.Agent, GoalTransitionReason.Admitted, version, default)).ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void Equality_WhenEveryFieldMatches_IsStructural()
    {
        var first = Build(GoalStatus.Proposed, GoalStatus.Ready);

        (first with { }).ShouldBe(first);
        first.ShouldNotBe(Build(GoalStatus.Proposed, GoalStatus.Ready));
    }

    private static GoalTransition Identified(GoalId goal, AgentId agent, SessionId session, RunId run, OperationId operation) => new(
        goal, agent, session, run, operation, GoalStatus.Proposed, GoalStatus.Ready, TransitionActor.Agent,
        GoalTransitionReason.Admitted, new VersionToken("1"), new IdempotencyKey("k"), GoalTestData.Now);

    private static GoalTransition Build(GoalStatus from, GoalStatus to) => new(
        new GoalId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
        new AgentId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
        new SessionId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
        new RunId(Guid.Parse("44444444-4444-4444-4444-444444444444")),
        new OperationId(Guid.NewGuid()),
        from,
        to,
        TransitionActor.Coordinator,
        GoalTransitionReason.Admitted,
        new VersionToken("1"),
        new IdempotencyKey("k"),
        GoalTestData.Now);
}
