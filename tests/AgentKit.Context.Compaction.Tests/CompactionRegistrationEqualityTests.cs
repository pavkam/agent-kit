// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

/// <summary>Verifies CompactionRegistrationEquality behavior and contracts.</summary>
public sealed class CompactionRegistrationEqualityTests
{
    private static readonly CompactionStrategyKey _a = new("a");
    private static readonly CompactionStrategyKey _b = new("b");

    [Fact]
    public void Equivalent_WhenEveryFieldMatches_ReturnsTrue() =>
        CompactionRegistrationEquality.Equivalent(Registration(), Registration()).ShouldBeTrue();

    [Fact]
    public void Equivalent_WhenBeforeListsHaveTheSameMembersInAnotherOrder_ReturnsTrue() =>
        CompactionRegistrationEquality.Equivalent(Registration(before: [_a, _b]), Registration(before: [_b, _a])).ShouldBeTrue();

    [Fact]
    public void Equivalent_WhenOrderDiffers_ReturnsFalse() =>
        CompactionRegistrationEquality.Equivalent(Registration(), Registration(order: 3)).ShouldBeFalse();

    [Fact]
    public void Equivalent_WhenLifetimeDiffers_ReturnsFalse() =>
        CompactionRegistrationEquality.Equivalent(Registration(), Registration(lifetime: ServiceLifetime.Transient)).ShouldBeFalse();

    [Fact]
    public void Equivalent_WhenBeforeDiffers_ReturnsFalse() =>
        CompactionRegistrationEquality.Equivalent(Registration(before: [_a]), Registration()).ShouldBeFalse();

    [Fact]
    public void Equivalent_WhenAfterDiffers_ReturnsFalse() =>
        CompactionRegistrationEquality.Equivalent(Registration(), Registration(after: [_a])).ShouldBeFalse();

    [Fact]
    public void Equivalent_WhenDescriptorVersionDiffers_ReturnsFalse() =>
        CompactionRegistrationEquality.Equivalent(Registration(), Registration(version: "2")).ShouldBeFalse();

    private static CompactionStrategyRegistration Registration(
        int order = 0,
        CompactionStrategyKey[]? before = null,
        CompactionStrategyKey[]? after = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton,
        string version = "1") =>
        new(
            new CompactionStrategyDescriptor(
                new CompactionStrategyKey("strategy"), new CompactionStrategyVersion(version), CompactionStrategyCapabilities.None, true, null),
            order,
            [.. before ?? []],
            [.. after ?? []],
            lifetime);
}
