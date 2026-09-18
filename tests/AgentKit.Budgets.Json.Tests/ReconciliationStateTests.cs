// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

/// <summary>Verifies the internal replay binding for one reconciliation key.</summary>
public sealed class ReconciliationStateTests
{
    /// <summary>Verifies reconciliation replay state reports the exact invalid constructor parameter.</summary>
    [Fact]
    public void Constructor_WhenArgumentIsNull_ThrowsWithExactParameterName()
    {
        Should.Throw<ArgumentNullException>(() => new ReconciliationState(null!, null!)).ParamName.ShouldBe("evidence");
        Should.Throw<ArgumentNullException>(() => new ReconciliationState(new BudgetStillUnknown(), null!)).ParamName.ShouldBe("result");
    }

    /// <summary>Verifies a valid construction exposes the exact captured evidence and result, and cloning preserves both.</summary>
    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesExactCapturedValuesAndClones()
    {
        var evidence = new BudgetStillUnknown();
        var reservation = new BudgetLedgerReservationReference(
            new BudgetLedgerScopeReference(
                new BudgetScopeId(Guid.NewGuid()),
                new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, null)),
            new BudgetReservationId(Guid.NewGuid()));
        var result = new BudgetLedgerReconciliationRetainedUnknown(reservation);

        var state = new ReconciliationState(evidence, result);

        state.Evidence.ShouldBeSameAs(evidence);
        state.Result.ShouldBeSameAs(result);
        var copy = state with { };
        copy.Evidence.ShouldBeSameAs(evidence);
        copy.Result.ShouldBeSameAs(result);
    }
}
