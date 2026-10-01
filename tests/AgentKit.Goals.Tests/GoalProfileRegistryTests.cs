// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

public sealed class GoalProfileRegistryTests
{
    private static readonly GoalProfileKey _key = new("profile");

    private static void Minimal(GoalProfileOptions options)
    {
        options.StoreKey = new GoalStoreKey("store");
        options.DispatcherKey = new DelegationDispatcherKey("dispatcher");
    }

    [Fact]
    public void Constructor_WhenHostIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new GoalProfileRegistry(null!)).ParamName.ShouldBe("host");

    [Fact]
    public void TryGet_WhenKeyIsUnknownOrBlank_ReturnsFalse()
    {
        var registry = new GoalProfileRegistry(new AgentGoalOptions());

        registry.TryGet(_key, out var unknown).ShouldBeFalse();
        registry.TryGet(default, out var blank).ShouldBeFalse();
        unknown.ShouldBeNull();
        blank.ShouldBeNull();
    }

    [Fact]
    public void TryGet_WhenConfigured_PublishesSnapshotWithBaselinePolicyFirstAndHostDefaults()
    {
        var registry = new GoalProfileRegistry(new AgentGoalOptions { MaximumDelegationDepth = 3 });
        var extra = new ComponentId("extra");
        registry.Configure(_key, options =>
        {
            Minimal(options);
            options.PolicyIds.Add(extra);
        }, replace: false);

        registry.TryGet(_key, out var snapshot).ShouldBeTrue();

        snapshot!.PolicyIds.ShouldBe([DenyUnlessAuthorizedDelegationPolicy.PolicyId, extra]);
        snapshot.MaximumDelegationDepth.ShouldBe(3);
        snapshot.StoreKey.Value.ShouldBe("store");
        snapshot.Fingerprint.Value.ShouldStartWith("sha256:");
    }

    [Fact]
    public void TryGet_WhenProfileUnchanged_ReturnsEqualFingerprintAndWhenChangedADifferentOne()
    {
        var registry = new GoalProfileRegistry(new AgentGoalOptions());
        registry.Configure(_key, Minimal, replace: false);
        _ = registry.TryGet(_key, out var first);
        _ = registry.TryGet(_key, out var again);
        registry.Configure(_key, static options => options.MaximumChildrenPerGoal = 2, replace: false);
        _ = registry.TryGet(_key, out var changed);

        again!.Fingerprint.ShouldBe(first!.Fingerprint);
        changed!.Fingerprint.ShouldNotBe(first.Fingerprint);
    }

    [Fact]
    public void Configure_WhenReplacing_DiscardsEarlierConfiguration()
    {
        var registry = new GoalProfileRegistry(new AgentGoalOptions());
        registry.Configure(_key, static options =>
        {
            Minimal(options);
            options.MaximumChildrenPerGoal = 2;
        }, replace: false);

        registry.Configure(_key, Minimal, replace: true);

        _ = registry.TryGet(_key, out var snapshot);
        snapshot!.MaximumChildrenPerGoal.ShouldBe(new AgentGoalOptions().MaximumChildrenPerGoal);
    }

    [Fact]
    public void Configure_WhenLimitExceedsHostCeiling_ThrowsArgumentOutOfRangeException()
    {
        var registry = new GoalProfileRegistry(new AgentGoalOptions { MaximumChildrenPerGoal = 2 });

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => registry.Configure(_key, static options =>
        {
            Minimal(options);
            options.MaximumChildrenPerGoal = 3;
        }, replace: false));

        exception.ParamName.ShouldBe("MaximumChildrenPerGoal");
    }

    [Fact]
    public void Configure_WhenStoreKeyIsMissing_ThrowsArgumentException()
    {
        var registry = new GoalProfileRegistry(new AgentGoalOptions());

        var exception = Should.Throw<ArgumentException>(() => registry.Configure(_key, static options => options.DispatcherKey = new DelegationDispatcherKey("d"), replace: false));

        exception.ParamName.ShouldBe("StoreKey");
    }

    [Fact]
    public void Configure_WhenDefaultJoinIsNotAllowed_ThrowsArgumentException()
    {
        var registry = new GoalProfileRegistry(new AgentGoalOptions());

        _ = Should.Throw<ArgumentException>(() => registry.Configure(_key, static options =>
        {
            Minimal(options);
            options.JoinStrategies.Clear();
            options.JoinStrategies.Add(GoalJoinStrategyKeys.Quorum);
            options.DefaultJoinStrategy = GoalJoinStrategyKeys.All;
        }, replace: false));
    }

    [Fact]
    public void Configure_WhenKeyIsBlankOrCallbackNull_ThrowsWithParameterName()
    {
        var registry = new GoalProfileRegistry(new AgentGoalOptions());

        Should.Throw<ArgumentException>(() => registry.Configure(default, Minimal, replace: false)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => registry.Configure(_key, null!, replace: false)).ParamName.ShouldBe("configure");
    }
}
