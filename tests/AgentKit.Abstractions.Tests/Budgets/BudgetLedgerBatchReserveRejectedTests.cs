// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetLedgerBatchReserveRejected behavior and contracts.</summary>
public sealed class BudgetLedgerBatchReserveRejectedTests
{
    [Fact]
    public void BudgetLedgerResultWrappers_WhenRequiredValueIsNull_ThrowArgumentNullException() => Should.Throw<ArgumentNullException>(() => new BudgetLedgerBatchReserveRejected(null!)).ParamName.ShouldBe("failure");

    [Fact]
    public void BudgetLedgerResultWrappers_WhenRequiredValueIsPresent_PreserveAllFields()
    {
        var scope = Scope();
        var limitFailure = LimitFailure(scope.Id);
        new BudgetLedgerBatchReserveRejected(limitFailure).Failure.ShouldBe(limitFailure);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new BudgetLedgerBatchReserveRejected(LimitFailure(Scope().Id));
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static BudgetLedgerScopeReference Scope(OperationId? operationId = null) => new(new BudgetScopeId(Guid.NewGuid()), Address(operationId));
    private static BudgetScopeAddress Address(OperationId? operationId = null) => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, operationId);
    private static BudgetLimitFailure LimitFailure(BudgetScopeId scopeId) => new(scopeId, new BudgetDimension("tests.requests"), BudgetLimitKind.Hard, 1m, 0m, 1m, new BudgetUnit("requests"), "safe");
}
