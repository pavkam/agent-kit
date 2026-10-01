// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

using static ToolRuntimeTestFixture;

/// <summary>Verifies ToolExecutionCapability behavior and contracts.</summary>
public sealed class ToolExecutionCapabilityTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var session = SessionCapability();
        var budget = BudgetCapability();
        var target = SessionTarget();
        var binding = new ToolExecutionPolicyBinding(ExecutionPolicy());
        var capability = new ToolExecutionCapability(session, budget, target, [binding]);
        capability.Session.ShouldBeSameAs(session);
        capability.Budget.ShouldBeSameAs(budget);
        capability.SessionTarget.ShouldBeSameAs(target);
        capability.ExecutionPolicies.ShouldBe([binding]);
        capability.Hooks.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenSessionIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolExecutionCapability(null!, BudgetCapability(), SessionTarget(), [])).ParamName.ShouldBe("session");

    [Fact]
    public void Constructor_WhenBudgetIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolExecutionCapability(SessionCapability(), null!, SessionTarget(), [])).ParamName.ShouldBe("budget");

    [Fact]
    public void Constructor_WhenSessionTargetIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolExecutionCapability(SessionCapability(), BudgetCapability(), null!, [])).ParamName.ShouldBe("sessionTarget");

    [Fact]
    public void Constructor_WhenPoliciesAreUninitialized_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolExecutionCapability(SessionCapability(), BudgetCapability(), SessionTarget(), default)).ParamName.ShouldBe("executionPolicies");

    [Fact]
    public void Constructor_WhenPoliciesContainNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolExecutionCapability(SessionCapability(), BudgetCapability(), SessionTarget(), [null!])).ParamName.ShouldBe("executionPolicies");

    [Fact]
    public void Constructor_WhenPolicyReferenceRepeats_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolExecutionCapability(
            SessionCapability(), BudgetCapability(), SessionTarget(),
            [new ToolExecutionPolicyBinding(ExecutionPolicy()), new ToolExecutionPolicyBinding(ExecutionPolicy())])).ParamName.ShouldBe("executionPolicies");

    [Fact]
    public void Equals_WhenBindingsDiffer_IsNotEqualAndEqualBindingsAreEqual()
    {
        var other = new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("other"), new ToolExecutionPolicyVersion(1));
        var session = SessionCapability();
        var budget = BudgetCapability();
        var target = SessionTarget();
        var left = new ToolExecutionCapability(session, budget, target, [new ToolExecutionPolicyBinding(ExecutionPolicy())]);
        var same = new ToolExecutionCapability(session, budget, target, [new ToolExecutionPolicyBinding(ExecutionPolicy())]);
        var right = new ToolExecutionCapability(session, budget, target, [new ToolExecutionPolicyBinding(other)]);

        left.Equals(same).ShouldBeTrue();
        left.GetHashCode().ShouldBe(same.GetHashCode());
        left.Equals(right).ShouldBeFalse();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = ExecutionCapability();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
