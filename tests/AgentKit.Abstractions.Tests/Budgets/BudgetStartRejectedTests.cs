// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetStartRejected behavior and contracts.</summary>
public sealed class BudgetStartRejectedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var failure = Failure();
        var rejected = new BudgetStartRejected(failure);
        rejected.Failure.ShouldBe(failure);
    }

    [Fact]
    public void Constructor_WhenFailureIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new BudgetStartRejected(null!));
        exception.ParamName.ShouldBe("failure");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new BudgetStartRejected(Failure());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static BudgetLimitFailure Failure() => new(new BudgetScopeId(Guid.NewGuid()), new BudgetDimension("tests.requests"), BudgetLimitKind.Hard, 1m, 0m, 1m, new BudgetUnit("requests"), "safe");
}
