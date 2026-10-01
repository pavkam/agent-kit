// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

/// <summary>Verifies GoalProfileSnapshot and GoalProfileReference constraints.</summary>
public sealed class GoalProfileSnapshotTests
{
    private static GoalProfileSnapshot Make(
        long version = 1,
        ImmutableArray<GoalJoinStrategyKey>? joins = null,
        GoalJoinStrategyKey? defaultJoin = null,
        int depth = 4,
        int children = 8,
        int attempts = 4,
        DelegationFailureMode mode = DelegationFailureMode.SettleAllChildren,
        ImmutableArray<ComponentId>? policies = null) => new(
            new GoalProfileKey("p"), new GoalProfileVersion(version), new GoalStoreKey("s"), new DelegationDispatcherKey("d"),
            policies ?? [], joins ?? [GoalJoinStrategyKeys.All], defaultJoin ?? GoalJoinStrategyKeys.All, depth, children, attempts, mode,
            new GoalBudget(10, 10, 2), new ContentHash("sha256:x"));

    [Fact]
    public void Constructor_WhenValid_ExposesAReferenceNamingThisExactRevision()
    {
        var snapshot = Make(3);

        snapshot.Reference.ShouldBe(new GoalProfileReference(new GoalProfileKey("p"), new GoalProfileVersion(3)));
    }

    [Fact]
    public void Constructor_WhenVersionIsNotPositive_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Make(0)).ParamName.ShouldBe("version");

    [Theory]
    [InlineData(0, 1, 1, "maximumDelegationDepth")]
    [InlineData(1, 0, 1, "maximumChildrenPerGoal")]
    [InlineData(1, 1, 0, "maximumConcurrentAttempts")]
    public void Constructor_WhenALimitIsNotPositive_ThrowsArgumentOutOfRangeExceptionNamingIt(int depth, int children, int attempts, string parameter) =>
        Should.Throw<ArgumentOutOfRangeException>(() => Make(depth: depth, children: children, attempts: attempts)).ParamName.ShouldBe(parameter);

    [Fact]
    public void Constructor_WhenTheDefaultJoinIsNotListed_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Make(joins: [GoalJoinStrategyKeys.Quorum])).ParamName.ShouldBe("defaultJoinStrategy");

    [Fact]
    public void Constructor_WhenNoJoinStrategyIsListed_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Make(joins: [])).ParamName.ShouldBe("joinStrategies");

    [Fact]
    public void Constructor_WhenFailureModeIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Make(mode: (DelegationFailureMode) 9)).ParamName.ShouldBe("failureMode");

    [Fact]
    public void Constructor_WhenAPolicyIdIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Make(policies: [default])).ParamName.ShouldBe("policyIds");

    [Fact]
    public void Equality_WhenCollectionsMatchByContent_IsStructural() => Make().ShouldBe(Make());

    [Fact]
    public void GoalProfileReference_WhenVersionIsNotPositive_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new GoalProfileReference(new GoalProfileKey("p"), new GoalProfileVersion(0))).ParamName.ShouldBe("version");
}
