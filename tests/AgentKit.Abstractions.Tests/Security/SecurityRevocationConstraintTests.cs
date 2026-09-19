// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies SecurityRevocationConstraint behavior and contracts.</summary>
public sealed class SecurityRevocationConstraintTests
{
    [Fact]
    public void Constructor_WhenTriggersIsDefault_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SecurityRevocationConstraint(new SecurityRevocationVersion(1), default)).ParamName.ShouldBe("triggers");

    [Fact]
    public void Constructor_WhenTriggersIsEmpty_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SecurityRevocationConstraint(new SecurityRevocationVersion(1), [])).ParamName.ShouldBe("triggers");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        ImmutableArray<SecurityRevocationTrigger> triggers = [SecurityRevocationTrigger.Explicit, SecurityRevocationTrigger.SessionClosed];
        var constraint = new SecurityRevocationConstraint(new SecurityRevocationVersion(1), triggers);
        constraint.Version.ShouldBe(new SecurityRevocationVersion(1));
        constraint.Triggers.ShouldBe(triggers);
    }

    [Fact]
    public void Equality_WhenTriggerSequencesMatch_InstancesAreStructurallyEqual()
    {
        var first = new SecurityRevocationConstraint(new SecurityRevocationVersion(1), [SecurityRevocationTrigger.Explicit]);
        var second = new SecurityRevocationConstraint(new SecurityRevocationVersion(1), [SecurityRevocationTrigger.Explicit]);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equality_WhenTriggerSequencesDiffer_InstancesAreNotEqual()
    {
        var first = new SecurityRevocationConstraint(new SecurityRevocationVersion(1), [SecurityRevocationTrigger.Explicit]);
        var second = new SecurityRevocationConstraint(new SecurityRevocationVersion(1), [SecurityRevocationTrigger.SessionClosed]);
        first.ShouldNotBe(second);
    }
}
