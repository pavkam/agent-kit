// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

using AgentKit.TestSupport;

/// <summary>Verifies AgentMessageRequest constraints.</summary>
public sealed class AgentMessageRequestTests
{
    private static readonly ImmutableArray<ContentPart> _parts = [new TextPart("hello", TextSemantics.Plain, ExtensionData.Empty)];

    private static AgentMessageRequest Create(
        GoalId? goal = null,
        GoalAttemptId? attempt = null,
        InputDelivery delivery = InputDelivery.Steer,
        ImmutableArray<ContentPart>? parts = null,
        string key = "k",
        ExecutionIdentity? identity = null,
        AgentId? sender = null,
        SessionId? recipientSession = null) => new(
            sender ?? GoalTestData.NewAgent(), GoalTestData.NewSession(), GoalTestData.NewRun(), GoalTestData.NewAgent(), recipientSession ?? GoalTestData.NewSession(),
            goal, attempt, delivery, parts ?? _parts, new IdempotencyKey(key), identity ?? GoalTestData.Identity());

    [Fact]
    public void Constructor_WhenValid_RetainsEveryFact()
    {
        var goal = new GoalId(Guid.NewGuid());
        var attempt = new GoalAttemptId(Guid.NewGuid());

        var request = Create(goal, attempt, InputDelivery.FollowUp);

        request.GoalId.ShouldBe(goal);
        request.AttemptId.ShouldBe(attempt);
        request.Delivery.ShouldBe(InputDelivery.FollowUp);
        request.Parts.ShouldBe(_parts);
        request.IdempotencyKey.Value.ShouldBe("k");
    }

    [Fact]
    public void Constructor_WhenAnIdentityIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var identity = GoalTestData.Identity();

        Should.Throw<ArgumentOutOfRangeException>(() => new AgentMessageRequest(default, GoalTestData.NewSession(), GoalTestData.NewRun(), GoalTestData.NewAgent(), GoalTestData.NewSession(), null, null, InputDelivery.Steer, _parts, new IdempotencyKey("k"), identity)).ParamName.ShouldBe("senderAgentId");
        Should.Throw<ArgumentOutOfRangeException>(() => new AgentMessageRequest(GoalTestData.NewAgent(), default, GoalTestData.NewRun(), GoalTestData.NewAgent(), GoalTestData.NewSession(), null, null, InputDelivery.Steer, _parts, new IdempotencyKey("k"), identity)).ParamName.ShouldBe("senderSessionId");
        Should.Throw<ArgumentOutOfRangeException>(() => new AgentMessageRequest(GoalTestData.NewAgent(), GoalTestData.NewSession(), default, GoalTestData.NewAgent(), GoalTestData.NewSession(), null, null, InputDelivery.Steer, _parts, new IdempotencyKey("k"), identity)).ParamName.ShouldBe("senderRunId");
        Should.Throw<ArgumentOutOfRangeException>(() => new AgentMessageRequest(GoalTestData.NewAgent(), GoalTestData.NewSession(), GoalTestData.NewRun(), default, GoalTestData.NewSession(), null, null, InputDelivery.Steer, _parts, new IdempotencyKey("k"), identity)).ParamName.ShouldBe("recipientAgentId");
        Should.Throw<ArgumentOutOfRangeException>(() => new AgentMessageRequest(GoalTestData.NewAgent(), GoalTestData.NewSession(), GoalTestData.NewRun(), GoalTestData.NewAgent(), default, null, null, InputDelivery.Steer, _parts, new IdempotencyKey("k"), identity)).ParamName.ShouldBe("recipientSessionId");
    }

    [Fact]
    public void Constructor_WhenAPresentCausalIdentityIsDefault_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Create(goal: default(GoalId))).ParamName.ShouldBe("goalId");
        Should.Throw<ArgumentOutOfRangeException>(() => Create(new GoalId(Guid.NewGuid()), default(GoalAttemptId))).ParamName.ShouldBe("attemptId");
    }

    [Fact]
    public void Constructor_WhenAttemptIsNamedWithoutAGoal_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Create(attempt: new GoalAttemptId(Guid.NewGuid()))).ParamName.ShouldBe("attemptId");

    [Fact]
    public void Constructor_WhenDeliveryIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Create(delivery: (InputDelivery) 99)).ParamName.ShouldBe("delivery");

    [Fact]
    public void Constructor_WhenPartsAreDefaultOrEmpty_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => Create(parts: default(ImmutableArray<ContentPart>))).ParamName.ShouldBe("parts");
        Should.Throw<ArgumentException>(() => Create(parts: ImmutableArray<ContentPart>.Empty)).ParamName.ShouldBe("parts");
    }

    [Fact]
    public void Constructor_WhenKeyIsTheDefaultValue_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new AgentMessageRequest(GoalTestData.NewAgent(), GoalTestData.NewSession(), GoalTestData.NewRun(), GoalTestData.NewAgent(), GoalTestData.NewSession(), null, null, InputDelivery.Steer, _parts, default, GoalTestData.Identity())).ParamName.ShouldBe("idempotencyKey");

    [Fact]
    public void Constructor_WhenIdentityIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentMessageRequest(GoalTestData.NewAgent(), GoalTestData.NewSession(), GoalTestData.NewRun(), GoalTestData.NewAgent(), GoalTestData.NewSession(), null, null, InputDelivery.Steer, _parts, new IdempotencyKey("k"), null!)).ParamName.ShouldBe("identity");

    [Fact]
    public void Equals_WhenEveryFactMatches_IsEqualWithTheSameHash()
    {
        var sender = GoalTestData.NewAgent();
        var recipientSession = GoalTestData.NewSession();
        var identity = GoalTestData.Identity();
        var first = new AgentMessageRequest(sender, recipientSession, new RunId(Guid.Parse("10000000-0000-0000-0000-000000000001")), sender, recipientSession, null, null, InputDelivery.Steer, _parts, new IdempotencyKey("k"), identity);
        var second = new AgentMessageRequest(sender, recipientSession, new RunId(Guid.Parse("10000000-0000-0000-0000-000000000001")), sender, recipientSession, null, null, InputDelivery.Steer, _parts, new IdempotencyKey("k"), identity);

        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }
}
