// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;

using AgentKit;

/// <summary>Verifies BudgetScopeResult behavior and contracts.</summary>
public sealed class BudgetScopeResultTests
{
    [Fact]
    public void BudgetScopeResult_CopyConstructor_WhenExternalVariantCopiesBuiltIn_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ForeignBudgetScopeResult(ScopeResult()));
        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void BudgetScopeResult_CopyConstructor_WhenOriginalIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ForeignBudgetScopeResult(null!));
        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void BudgetScopeResult_CopyConstructor_WhenBuiltInVariantCopies_PreservesConcreteTypeAndValue()
    {
        var original = ScopeResult();
        var copy = original with
        {
        };
        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
        copy.GetType().ShouldBe(typeof(BudgetScopeCreationFailed));
        copy.Kind.ShouldBe(original.Kind);
        copy.SafeMessage.ShouldBe(original.SafeMessage);
    }

    private static BudgetScopeCreationFailed ScopeResult() => new(BudgetScopeCreationFailureKind.InvalidLimit, "The configured limit is invalid.");
    private sealed record ForeignBudgetScopeResult(BudgetScopeResult Original): BudgetScopeResult(Original);
}
