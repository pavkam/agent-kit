// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

/// <summary>Verifies GoalEvent constraints.</summary>
public sealed class GoalEventTests
{
    private static GoalEvent Make(
        GoalEventKind kind = GoalEventKind.GoalTransitioned,
        GoalId? goal = null,
        TenantId? tenant = null,
        GoalStatus? from = GoalStatus.Proposed,
        GoalStatus? to = GoalStatus.Ready) => new(
            kind, goal ?? new GoalId(Guid.NewGuid()), null, tenant ?? new TenantId("t"), new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()),
            new GoalProfileKey("p"), from, to, null, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Make((GoalEventKind) 99)).ParamName.ShouldBe("kind");

    [Fact]
    public void Constructor_WhenGoalIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Make(goal: default(GoalId))).ParamName.ShouldBe("goalId");

    [Fact]
    public void Constructor_WhenTenantIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Make(tenant: default(TenantId))).ParamName.ShouldBe("tenantId");

    [Fact]
    public void Constructor_WhenAStatusIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Make(from: (GoalStatus) 99)).ParamName.ShouldBe("from");
        Should.Throw<ArgumentOutOfRangeException>(() => Make(to: (GoalStatus) 99)).ParamName.ShouldBe("to");
    }

    [Fact]
    public void Constructor_WhenStatusesAreAbsent_IsAccepted() => Make(GoalEventKind.DelegationDispatched, from: null, to: null).From.ShouldBeNull();
}
