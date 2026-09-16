// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetUnresolvedReservationQuery behavior and contracts.</summary>
public sealed class BudgetUnresolvedReservationQueryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BudgetUnresolvedReservationQuery_WhenPageSizeIsOutsideBound_ThrowsExactParameterName(int pageSize)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetUnresolvedReservationQuery(Scope(), pageSize, null));
        exception.ParamName.ShouldBe("pageSize");
    }

    [Fact]
    public void BudgetUnresolvedReservationQuery_WhenCursorBelongsToAnotherScope_ThrowsExactParameterName()
    {
        var firstScope = Scope();
        var otherScope = Scope();
        var cursor = new BudgetReservationCursor(firstScope, new BudgetLedgerWatermark(1), ReservationId());
        var exception = Should.Throw<ArgumentException>(() => new BudgetUnresolvedReservationQuery(otherScope, 1, cursor));
        exception.ParamName.ShouldBe("after");
    }

    [Fact]
    public void BudgetUnresolvedReservationQuery_WhenScopeIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new BudgetUnresolvedReservationQuery(null!, 1, null));
        exception.ParamName.ShouldBe("scope");
    }

    [Fact]
    public void BudgetUnresolvedReservationQuery_WhenInputsAreValid_PreservesAllFields()
    {
        var scope = Scope();
        var cursor = new BudgetReservationCursor(scope, new BudgetLedgerWatermark(1), ReservationId());
        var query = new BudgetUnresolvedReservationQuery(scope, 1, cursor);
        query.Scope.ShouldBe(scope);
        query.PageSize.ShouldBe(1);
        query.After.ShouldBe(cursor);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var scope = Scope();
        var original = new BudgetUnresolvedReservationQuery(scope, 1, new BudgetReservationCursor(scope, new BudgetLedgerWatermark(1), ReservationId()));
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static BudgetLedgerScopeReference Scope(OperationId? operationId = null) => new(new BudgetScopeId(Guid.NewGuid()), Address(operationId));
    private static BudgetScopeAddress Address(OperationId? operationId = null) => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, operationId);
    private static BudgetReservationId ReservationId() => new(Guid.NewGuid());
}
