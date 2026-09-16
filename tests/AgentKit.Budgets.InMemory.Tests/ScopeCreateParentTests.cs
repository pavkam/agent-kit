// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.InMemory.Tests;

using AgentKit.Budgets.Storage;

/// <summary>Verifies ScopeCreateParent behavior and contracts.</summary>
public sealed class ScopeCreateParentTests
{
    /// <summary>Verifies a valid construction exposes exactly its captured evidence.</summary>
    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesExactCapturedValues()
    {
        var reference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), TestFactory().Address);
        var request = TestFactory().Request;
        var ancestors = ImmutableArray.Create(request);
        var parent = new ScopeCreateParent(reference, request, 1, ancestors);
        parent.Reference.ShouldBeSameAs(reference);
        parent.Request.ShouldBeSameAs(request);
        parent.Depth.ShouldBe(1);
        parent.Ancestors.ShouldBe(ancestors);
    }

    /// <summary>Verifies record equality and with-expression cloning preserve every captured field.</summary>
    [Fact]
    public void WithExpression_WhenNoFieldChanges_ClonesEveryField()
    {
        var reference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), TestFactory().Address);
        var request = TestFactory().Request;
        var original = new ScopeCreateParent(reference, request, 1, [request]);
        var copy = original with { };
        copy.ShouldNotBeSameAs(original);
        copy.ShouldBe(original);
    }

    private static (BudgetScopeAddress Address, BudgetLedgerScopeCreateRequest Request) TestFactory()
    {
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, null);
        var request = new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(null, address, [], new IdempotencyKey("scope-create-parent-test")),
            new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5)));
        return (address, request);
    }
}
