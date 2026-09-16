// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetScopeCreated behavior and contracts.</summary>
public sealed class BudgetScopeCreatedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var scope = Scope();
        var created = new BudgetScopeCreated(scope);
        created.Scope.ShouldBeSameAs(scope);
    }

    [Fact]
    public void Constructor_WhenScopeIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new BudgetScopeCreated(null!));
        exception.ParamName.ShouldBe("scope");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var scope = Scope();
        var original = new BudgetScopeCreated(scope);
        var copy = original with { };
        copy.Scope.ShouldBeSameAs(scope);
    }

    private static TestBudgetScope Scope() => new(new BudgetScopeId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, null));
}
