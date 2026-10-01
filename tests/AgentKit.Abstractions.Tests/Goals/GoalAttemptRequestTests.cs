// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

using AgentKit.TestSupport;

/// <summary>Verifies GoalAttemptRequest constraints.</summary>
public sealed class GoalAttemptRequestTests
{
    private static GoalAttemptRequest Make(
        GoalId? goal = null,
        int number = 1,
        GoalAttemptId? attemptId = null,
        RunId? attemptRun = null,
        RunId? requestingRun = null,
        OperationId? operation = null,
        TransitionActor actor = TransitionActor.Worker,
        string key = "k",
        VersionToken? version = null,
        SecurityAuthorizationContext? authorization = null) => new(
            GoalTestData.Profile,
            goal ?? new GoalId(Guid.NewGuid()),
            version ?? new VersionToken("1"),
            number,
            attemptId,
            GoalTestData.NewAgent(),
            GoalTestData.NewSession(),
            attemptRun ?? GoalTestData.NewRun(),
            requestingRun ?? GoalTestData.NewRun(),
            operation ?? GoalTestData.NewOperation(),
            actor,
            new GoalBudgetReservation(new GoalBudget(1, 1, 0)),
            new IdempotencyKey(key),
            authorization ?? GoalTestData.Authorization(GoalTestData.NewAgent(), GoalTestData.NewSession(), GoalTestData.NewRun()));

    [Fact]
    public void Constructor_WhenValid_RetainsTheOptionalAttemptIdentity()
    {
        var id = new GoalAttemptId(Guid.NewGuid());

        Make(attemptId: id).AttemptId.ShouldBe(id);
        Make().AttemptId.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenAnIdentityIsDefault_ThrowsArgumentOutOfRangeExceptionNamingIt()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Make(goal: default(GoalId))).ParamName.ShouldBe("goalId");
        Should.Throw<ArgumentOutOfRangeException>(() => Make(attemptId: default(GoalAttemptId))).ParamName.ShouldBe("attemptId");
        Should.Throw<ArgumentOutOfRangeException>(() => Make(attemptRun: default(RunId))).ParamName.ShouldBe("attemptRunId");
        Should.Throw<ArgumentOutOfRangeException>(() => Make(requestingRun: default(RunId))).ParamName.ShouldBe("requestingRunId");
        Should.Throw<ArgumentOutOfRangeException>(() => Make(operation: default(OperationId))).ParamName.ShouldBe("operationId");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void Constructor_WhenTheAttemptNumberIsNotPositive_ThrowsArgumentOutOfRangeException(int number) =>
        Should.Throw<ArgumentOutOfRangeException>(() => Make(number: number)).ParamName.ShouldBe("attemptNumber");

    [Fact]
    public void Constructor_WhenTheActorIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Make(actor: (TransitionActor) 99)).ParamName.ShouldBe("actor");

    [Fact]
    public void Constructor_WhenTheVersionTokenIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Make(version: default(VersionToken))).ParamName.ShouldBe("expectedVersion");
}
