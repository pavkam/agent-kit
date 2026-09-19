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
        var capability = new ToolExecutionCapability(session, budget);
        capability.Session.ShouldBeSameAs(session);
        capability.Budget.ShouldBeSameAs(budget);
    }

    [Fact]
    public void Constructor_WhenSessionIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolExecutionCapability(null!, BudgetCapability())).ParamName.ShouldBe("session");

    [Fact]
    public void Constructor_WhenBudgetIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolExecutionCapability(SessionCapability(), null!)).ParamName.ShouldBe("budget");

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = ExecutionCapability();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
