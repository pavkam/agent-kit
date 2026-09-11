// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetReservationCursor behavior and contracts.</summary>
public sealed class BudgetReservationCursorTests
{
    [Fact]
    public void BudgetReservationCursor_WhenScopeOrCoordinatesAreInvalid_ThrowsExactExceptionAndParameterName()
    {
        var nullScope = Should.Throw<ArgumentNullException>(() => new BudgetReservationCursor(null!, new BudgetLedgerWatermark(1), ReservationId()));
        var defaultWatermark = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetReservationCursor(Scope(), default, ReservationId()));
        var defaultReservation = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetReservationCursor(Scope(), new BudgetLedgerWatermark(1), default));
        nullScope.ParamName.ShouldBe("scope");
        defaultWatermark.ParamName.ShouldBe("watermark");
        defaultReservation.ParamName.ShouldBe("afterReservationId");
    }

    [Fact]
    public void BudgetReservationCursor_WhenCoordinatesAreValid_PreservesAllFields()
    {
        var scope = Scope();
        var watermark = new BudgetLedgerWatermark(1);
        var reservationId = ReservationId();
        var cursor = new BudgetReservationCursor(scope, watermark, reservationId);
        cursor.Scope.ShouldBe(scope);
        cursor.Watermark.ShouldBe(watermark);
        cursor.AfterReservationId.ShouldBe(reservationId);
    }

    private static BudgetLedgerScopeReference Scope(OperationId? operationId = null) => new(new BudgetScopeId(Guid.NewGuid()), Address(operationId));
    private static BudgetScopeAddress Address(OperationId? operationId = null) => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, operationId);
    private static BudgetReservationId ReservationId() => new(Guid.NewGuid());
}
