// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

/// <summary>Verifies <see cref="ContextBudget"/> invariants.</summary>
public sealed class ContextBudgetTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_PreservesEvidence()
    {
        var budget = new ContextBudget(8_192, 1_024, 256, 0.10);
        budget.ReservedOutputTokens.ShouldBe(1_024);
    }

    [Fact]
    public void Constructor_WhenSafetyMarginIsGreaterThanOne_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ContextBudget(100, 0, 0, 1.1)).ParamName.ShouldBe("estimationSafetyMargin");
}
