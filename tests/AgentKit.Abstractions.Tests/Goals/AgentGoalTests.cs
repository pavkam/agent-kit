// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

using AgentKit.TestSupport;

/// <summary>Verifies AgentGoal constraints and state derivation.</summary>
public sealed class AgentGoalTests
{
    private static AgentGoal Make(
        GoalStatus status = GoalStatus.Proposed,
        GoalAttemptId? attempt = null,
        GoalId? id = null,
        GoalId? parent = null,
        GoalProfileVersion? profileVersion = null,
        string version = "1",
        AgentDefinitionRevision? revision = null) => new(
            id ?? new GoalId(Guid.NewGuid()),
            parent,
            GoalTestData.NewAgent(),
            GoalTestData.NewSession(),
            GoalTestData.NewRun(),
            new GoalProfileKey("p"),
            profileVersion ?? new GoalProfileVersion(1),
            revision ?? new AgentDefinitionRevision(0),
            status,
            new GoalDefinition("o", [], ExtensionData.Empty),
            new GoalBudget(1, 1, 0),
            attempt,
            new VersionToken(version),
            GoalTestData.Now,
            ExtensionData.Empty);

    [Fact]
    public void Constructor_WhenParentEqualsTheGoal_ThrowsArgumentOutOfRangeException()
    {
        var id = new GoalId(Guid.NewGuid());

        Should.Throw<ArgumentOutOfRangeException>(() => Make(id: id, parent: id)).ParamName.ShouldBe("parentId");
    }

    [Fact]
    public void Constructor_WhenProfileVersionIsNotPositive_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Make(profileVersion: new GoalProfileVersion(0))).ParamName.ShouldBe("profileVersion");

    [Fact]
    public void Constructor_WhenDefinitionRevisionIsZero_IsAccepted() => Make(revision: new AgentDefinitionRevision(0)).AgentDefinitionRevision.Value.ShouldBe(0);

    [Fact]
    public void Constructor_WhenActiveWithoutAnAttempt_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Make(GoalStatus.Active)).ParamName.ShouldBe("activeAttemptId");

    [Theory]
    [InlineData(GoalStatus.Proposed)]
    [InlineData(GoalStatus.Ready)]
    public void Constructor_WhenPreStartStatusNamesAnAttempt_ThrowsArgumentException(GoalStatus status) =>
        Should.Throw<ArgumentException>(() => Make(status, new GoalAttemptId(Guid.NewGuid()))).ParamName.ShouldBe("activeAttemptId");

    [Fact]
    public void Constructor_WhenStatusIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Make((GoalStatus) 99)).ParamName.ShouldBe("status");

    [Fact]
    public void Constructor_WhenVersionIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new AgentGoal(
            new GoalId(Guid.NewGuid()), null, GoalTestData.NewAgent(), GoalTestData.NewSession(), GoalTestData.NewRun(),
            new GoalProfileKey("p"), new GoalProfileVersion(1), new AgentDefinitionRevision(0), GoalStatus.Proposed,
            new GoalDefinition("o", [], ExtensionData.Empty), new GoalBudget(1, 1, 0), null, default, GoalTestData.Now, ExtensionData.Empty))
            .ParamName.ShouldBe("version");

    [Fact]
    public void WithState_WhenCalled_PreservesEveryCreationFactAndChangesOnlyStateAndVersion()
    {
        var goal = Make();
        var attempt = new GoalAttemptId(Guid.NewGuid());

        var next = goal.WithState(GoalStatus.Active, attempt, new VersionToken("2"));

        next.Status.ShouldBe(GoalStatus.Active);
        next.ActiveAttemptId.ShouldBe(attempt);
        next.Version.ShouldBe(new VersionToken("2"));
        next.Id.ShouldBe(goal.Id);
        next.OwnerAgentId.ShouldBe(goal.OwnerAgentId);
        next.Definition.ShouldBe(goal.Definition);
        next.CreatedAt.ShouldBe(goal.CreatedAt);
        goal.Status.ShouldBe(GoalStatus.Proposed);
    }

    [Fact]
    public void WithState_WhenTheNewStateViolatesAnInvariant_Throws() =>
        Should.Throw<ArgumentException>(() => Make().WithState(GoalStatus.Active, null, new VersionToken("2")));
}
