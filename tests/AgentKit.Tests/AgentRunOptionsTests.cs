// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using System.Collections.Immutable;

/// <summary>Verifies AgentRunOptions behavior and contracts.</summary>
public sealed class AgentRunOptionsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaxTurnsIsZeroOrNegative_ThrowsExactArgumentOutOfRangeException(int maxTurns)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new AgentRunOptions(maxTurns));

        exception.ParamName.ShouldBe("maxTurns");
    }

    [Fact]
    public void Constructor_WhenAttemptTimeoutIsZeroOrNegative_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new AgentRunOptions(attemptTimeout: TimeSpan.Zero));

        exception.ParamName.ShouldBe("attemptTimeout");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreOmitted_DefaultsToNull()
    {
        var options = new AgentRunOptions();

        options.MaxTurns.ShouldBeNull();
        options.AttemptTimeout.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var options = new AgentRunOptions(3, TimeSpan.FromMinutes(1));

        options.MaxTurns.ShouldBe(3);
        options.AttemptTimeout.ShouldBe(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Constructor_WhenRestrictionsAreOmitted_DefaultsToNull()
    {
        var options = new AgentRunOptions();

        options.AllowedTools.ShouldBeNull();
        options.BudgetParentScopeId.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenRestrictionsAreValid_RoundTripsThemIncludingAnEmptyAllowList()
    {
        var parent = new BudgetScopeId(Guid.Parse("e0000000-0000-0000-0000-000000000002"));

        var options = new AgentRunOptions(allowedTools: [], budgetParentScopeId: parent);

        options.AllowedTools.ShouldNotBeNull().ShouldBeEmpty();
        options.BudgetParentScopeId.ShouldBe(parent);
    }

    [Fact]
    public void Constructor_WhenAllowListIsDefaultOrHoldsADefaultTool_ThrowsTheExactException()
    {
        Should.Throw<ArgumentException>(() => new AgentRunOptions(allowedTools: default(ImmutableArray<ToolId>))).ParamName.ShouldBe("allowedTools");
        Should.Throw<ArgumentOutOfRangeException>(() => new AgentRunOptions(allowedTools: [default])).ParamName.ShouldBe("allowedTools");
    }

    [Fact]
    public void Constructor_WhenBudgetParentScopeIsDefault_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new AgentRunOptions(budgetParentScopeId: default(BudgetScopeId))).ParamName.ShouldBe("budgetParentScopeId");

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = CompositionTestData.RunOptions(maxTurns: 2);
        var copy = original with { };

        copy.ShouldBe(original);
    }
}
