// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.InMemory.Tests;



/// <summary>Verifies BatchState behavior and contracts.</summary>
public sealed class BatchStateTests
{
    /// <summary>Verifies the batch replay state reports the exact invalid constructor parameter.</summary>
    [Fact]
    public void BatchState_WhenArgumentIsNull_ThrowsWithExactParameterName()
    {
        var requestException = Should.Throw<ArgumentNullException>(() => new BatchState(null!, null!));
        requestException.ParamName.ShouldBe("request");
        var resultException = Should.Throw<ArgumentNullException>(() => new BatchState(CreateBatchRequest(), null!));
        resultException.ParamName.ShouldBe("result");
    }

    private static BudgetLedgerBatchReserveRequest CreateBatchRequest()
    {
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.Parse("30000000-0000-0000-0000-000000000001")), null, null, null);
        var scope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.Parse("10000000-0000-0000-0000-000000000001")), address);
        var request = new BudgetReservationRequest(scope.Id, new BudgetDimension("tokens"), 1, new BudgetUnit("count"), new OperationId(Guid.Parse("20000000-0000-0000-0000-000000000001")), null, new IdempotencyKey("state-test"));
        return new BudgetLedgerBatchReserveRequest(scope, [request]);
    }
}
