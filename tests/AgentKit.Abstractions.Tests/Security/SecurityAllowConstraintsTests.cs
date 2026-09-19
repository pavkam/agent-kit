// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies SecurityAllowConstraints behavior and contracts.</summary>
public sealed class SecurityAllowConstraintsTests
{
    [Fact]
    public void Constructor_WhenResourcesIsEmpty_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SecurityAllowConstraints(ImmutableArray<ProtectedResource>.Empty, null, null, null, null)).ParamName.ShouldBe("resources");

    [Fact]
    public void Constructor_WhenEffectIsUndefined_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SecurityAllowConstraints(null, (SecurityEffect) 99, null, null, null)).ParamName.ShouldBe("effect");

    [Fact]
    public void Constructor_WhenAllowedUsesIsNotPositive_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SecurityAllowConstraints(null, null, null, null, 0)).ParamName.ShouldBe("allowedUses");

    [Fact]
    public void Constructor_WhenExpiresAtIsNotLaterThanNotBefore_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SecurityAllowConstraints(null, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null)).ParamName.ShouldBe("expiresAt");

    [Fact]
    public void Constructor_WhenEveryMemberIsNull_RoundTripsAsNoNarrowing()
    {
        var constraints = new SecurityAllowConstraints(null, null, null, null, null);
        constraints.Resources.ShouldBeNull();
        constraints.Effect.ShouldBeNull();
        constraints.NotBefore.ShouldBeNull();
        constraints.ExpiresAt.ShouldBeNull();
        constraints.AllowedUses.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        ImmutableArray<ProtectedResource> resources = [SecurityAbstractionsTestData.Resource()];
        var constraints = new SecurityAllowConstraints(resources, SecurityEffect.Observe, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), 3);
        constraints.Resources.ShouldBe(resources);
        constraints.Effect.ShouldBe(SecurityEffect.Observe);
        constraints.NotBefore.ShouldBe(DateTimeOffset.UnixEpoch);
        constraints.ExpiresAt.ShouldBe(DateTimeOffset.UnixEpoch.AddMinutes(1));
        constraints.AllowedUses.ShouldBe(3);
    }

    [Fact]
    public void Equality_WhenResourceSequencesMatch_InstancesAreStructurallyEqual()
    {
        var resources = ImmutableArray.Create(SecurityAbstractionsTestData.Resource());
        var first = new SecurityAllowConstraints(resources, null, null, null, null);
        var second = new SecurityAllowConstraints(resources, null, null, null, null);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equality_WhenOneResourcesIsNullAndOtherIsNot_InstancesAreNotEqual()
    {
        var first = new SecurityAllowConstraints(null, null, null, null, null);
        var second = new SecurityAllowConstraints(ImmutableArray.Create(SecurityAbstractionsTestData.Resource()), null, null, null, null);
        first.ShouldNotBe(second);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SecurityAllowConstraints(ImmutableArray.Create(SecurityAbstractionsTestData.Resource()), SecurityEffect.Observe, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), 1);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
