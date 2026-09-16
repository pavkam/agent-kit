// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Delegation;



/// <summary>Verifies TaskDelegationBudget behavior and contracts.</summary>
public sealed class TaskDelegationBudgetTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var budget = new TaskDelegationBudget(5, 10);
        budget.MaximumTurns.ShouldBe(5);
        budget.MaximumToolCalls.ShouldBe(10);
    }

    [Fact]
    public void Constructor_WhenMaximumTurnsIsNotPositive_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new TaskDelegationBudget(0, 10));
        exception.ParamName.ShouldBe("maximumTurns");
    }

    [Fact]
    public void Constructor_WhenMaximumToolCallsIsNotPositive_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new TaskDelegationBudget(5, 0));
        exception.ParamName.ShouldBe("maximumToolCalls");
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var first = new TaskDelegationBudget(5, 10);
        var second = new TaskDelegationBudget(5, 10);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesIndependentCopy()
    {
        var original = new TaskDelegationBudget(5, 10);
        var copy = original with { MaximumTurns = 6 };
        copy.MaximumTurns.ShouldBe(6);
        original.MaximumTurns.ShouldBe(5);
    }
}
