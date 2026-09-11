// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetLedgerScopeCreated behavior and contracts.</summary>
public sealed class BudgetLedgerScopeCreatedTests
{
    [Fact]
    public void BudgetLedgerResultWrappers_WhenRequiredValueIsNull_ThrowArgumentNullException() => Should.Throw<ArgumentNullException>(() => new BudgetLedgerScopeCreated(null!)).ParamName.ShouldBe("scope");

    [Fact]
    public void BudgetLedgerResultWrappers_WhenRequiredValueIsPresent_PreserveAllFields()
    {
        var scope = Scope();
        new BudgetLedgerScopeCreated(scope).Scope.ShouldBe(scope);
    }

    private static BudgetLedgerScopeReference Scope(OperationId? operationId = null) => new(new BudgetScopeId(Guid.NewGuid()), Address(operationId));
    private static BudgetScopeAddress Address(OperationId? operationId = null) => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, operationId);
}
