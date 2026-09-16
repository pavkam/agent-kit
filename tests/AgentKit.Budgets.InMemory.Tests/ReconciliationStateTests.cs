// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.InMemory.Tests;



/// <summary>Verifies ReconciliationState behavior and contracts.</summary>
public sealed class ReconciliationStateTests
{
    /// <summary>Verifies reconciliation replay state reports the exact invalid constructor parameter.</summary>
    [Fact]
    public void ReconciliationState_WhenArgumentIsNull_ThrowsWithExactParameterName()
    {
        var evidenceException = Should.Throw<ArgumentNullException>(() => new ReconciliationState(null!, null!));
        evidenceException.ParamName.ShouldBe("evidence");
        var resultException = Should.Throw<ArgumentNullException>(() => new ReconciliationState(new BudgetStillUnknown(), null!));
        resultException.ParamName.ShouldBe("result");
    }

    /// <summary>Verifies a valid construction exposes the exact captured evidence and result.</summary>
    [Fact]
    public void ReconciliationState_WhenArgumentsAreValid_ExposesExactCapturedValues()
    {
        var evidence = new BudgetStillUnknown();
        var reservation = new BudgetLedgerReservationReference(
            new BudgetLedgerScopeReference(new BudgetScopeId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.Parse("30000000-0000-0000-0000-000000000001")), null, null, null)),
            new BudgetReservationId(Guid.Parse("20000000-0000-0000-0000-000000000001")));
        var result = new BudgetLedgerReconciliationRetainedUnknown(reservation);
        var state = new ReconciliationState(evidence, result);
        state.Evidence.ShouldBeSameAs(evidence);
        state.Result.ShouldBeSameAs(result);
        var copy = state with { };
        copy.Evidence.ShouldBeSameAs(evidence);
        copy.Result.ShouldBeSameAs(result);
    }
}
