// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetLedgerReservationReference behavior and contracts.</summary>
public sealed class BudgetLedgerReservationReferenceTests
{
    [Fact]
    public void BudgetLedgerReservationReference_WhenScopeIsNullOrIdIsDefault_ThrowsExactExceptionAndParameterName()
    {
        var nullScope = Should.Throw<ArgumentNullException>(() => new BudgetLedgerReservationReference(null!, ReservationId()));
        var defaultId = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetLedgerReservationReference(Scope(), default));
        nullScope.ParamName.ShouldBe("scope");
        defaultId.ParamName.ShouldBe("id");
    }

    [Fact]
    public void BudgetLedgerReservationReference_WhenInputsAreValid_PreservesAllFields()
    {
        var scope = Scope();
        var id = ReservationId();
        var reference = new BudgetLedgerReservationReference(scope, id);
        reference.Scope.ShouldBe(scope);
        reference.Id.ShouldBe(id);
    }

    private static BudgetLedgerScopeReference Scope(OperationId? operationId = null) => new(new BudgetScopeId(Guid.NewGuid()), Address(operationId));
    private static BudgetScopeAddress Address(OperationId? operationId = null) => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, operationId);
    private static BudgetReservationId ReservationId() => new(Guid.NewGuid());
}
