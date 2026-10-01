// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

/// <summary>Verifies CompactionStrategyOrdering behavior and contracts.</summary>
public sealed class CompactionStrategyOrderingTests
{
    private static readonly CompactionProfileKey _profile = new("ordering-profile");
    private static readonly CompactionStrategyKey _a = new("a");
    private static readonly CompactionStrategyKey _b = new("b");
    private static readonly CompactionStrategyKey _c = new("c");

    [Fact]
    public void Validate_WhenThereAreNoConstraints_Accepts() =>
        Should.NotThrow(() => CompactionStrategyOrdering.Validate(_profile, [Registration(_a), Registration(_b)], [_b, _a]));

    [Fact]
    public void Validate_WhenBeforeIsHonored_Accepts() =>
        Should.NotThrow(() => CompactionStrategyOrdering.Validate(
            _profile, [Registration(_a), Registration(_b, before: [_a])], [_a, _b]));

    [Fact]
    public void Validate_WhenBeforeIsContradicted_ThrowsInvalidOperationException()
    {
        var failure = Should.Throw<InvalidOperationException>(() => CompactionStrategyOrdering.Validate(
            _profile, [Registration(_a), Registration(_b, before: [_a])], [_b, _a]));

        failure.Message.ShouldContain("contradicts");
    }

    [Fact]
    public void Validate_WhenAfterIsContradicted_ThrowsInvalidOperationException() =>
        Should.Throw<InvalidOperationException>(() => CompactionStrategyOrdering.Validate(
            _profile, [Registration(_a, after: [_b]), Registration(_b)], [_b, _a]));

    [Fact]
    public void Validate_WhenAConstrainedPairIsNotBothOrdered_IgnoresTheConstraint() =>
        Should.NotThrow(() => CompactionStrategyOrdering.Validate(
            _profile, [Registration(_a), Registration(_b, before: [_a])], [_b]));

    [Fact]
    public void Validate_WhenAConstraintNamesAnAbsentStrategy_IgnoresIt() =>
        Should.NotThrow(() => CompactionStrategyOrdering.Validate(_profile, [Registration(_a, before: [_c])], [_a]));

    [Fact]
    public void Validate_WhenConstraintsFormACycle_ThrowsInvalidOperationExceptionNamingTheProfile()
    {
        var failure = Should.Throw<InvalidOperationException>(() => CompactionStrategyOrdering.Validate(
            _profile, [Registration(_a, before: [_b]), Registration(_b, before: [_a])], [_a]));

        failure.Message.ShouldContain("cycle");
        failure.Message.ShouldContain(_profile.Value);
    }

    [Fact]
    public void Validate_WhenALongerCycleExists_ThrowsInvalidOperationException() =>
        Should.Throw<InvalidOperationException>(() => CompactionStrategyOrdering.Validate(
            _profile,
            [Registration(_a, before: [_b]), Registration(_b, before: [_c]), Registration(_c, before: [_a])],
            [_a]));

    [Fact]
    public void Validate_WhenAnAcyclicChainExistsRegardlessOfNumericOrder_Accepts() =>
        Should.NotThrow(() => CompactionStrategyOrdering.Validate(
            _profile,
            [Registration(_a, order: 9), Registration(_b, order: 1, before: [_a]), Registration(_c, order: 5, before: [_b])],
            [_a, _b, _c]));

    private static CompactionStrategyRegistration Registration(
        CompactionStrategyKey key,
        int order = 0,
        CompactionStrategyKey[]? before = null,
        CompactionStrategyKey[]? after = null) =>
        new(
            new CompactionStrategyDescriptor(key, new CompactionStrategyVersion("1"), CompactionStrategyCapabilities.None, true, null),
            order,
            [.. before ?? []],
            [.. after ?? []],
            ServiceLifetime.Singleton);
}
