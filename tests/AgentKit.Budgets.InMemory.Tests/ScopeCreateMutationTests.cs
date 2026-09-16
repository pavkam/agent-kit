// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.InMemory.Tests;

using AgentKit.Budgets.Storage;

/// <summary>Verifies ScopeCreateMutation behavior and contracts.</summary>
public sealed class ScopeCreateMutationTests
{
    /// <summary>Verifies a valid construction exposes exactly its captured evidence.</summary>
    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesExactCapturedValues()
    {
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, null);
        var reference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), address);
        var request = new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(null, address, [], new IdempotencyKey("scope-create-mutation-test")),
            new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5)));
        var parentId = new BudgetScopeId(Guid.NewGuid());
        var mutation = new ScopeCreateMutation(reference, request, parentId, 2, 5);
        mutation.Reference.ShouldBeSameAs(reference);
        mutation.Request.ShouldBeSameAs(request);
        mutation.ParentScopeId.ShouldBe(parentId);
        mutation.Depth.ShouldBe(2);
        mutation.Revision.ShouldBe(5);
    }

    /// <summary>Verifies record equality and with-expression cloning preserve every captured field.</summary>
    [Fact]
    public void WithExpression_WhenNoFieldChanges_ClonesEveryField()
    {
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, null);
        var reference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), address);
        var request = new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(null, address, [], new IdempotencyKey("scope-create-mutation-with-test")),
            new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5)));
        var original = new ScopeCreateMutation(reference, request, null, 1, 1);
        var copy = original with { };
        copy.ShouldNotBeSameAs(original);
        copy.ShouldBe(original);
    }
}
