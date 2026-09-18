// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

/// <summary>Verifies the internal replay binding for one indivisible reservation batch.</summary>
public sealed class BatchStateTests
{
    /// <summary>Verifies the batch replay state reports the exact invalid constructor parameter.</summary>
    [Fact]
    public void Constructor_WhenArgumentIsNull_ThrowsWithExactParameterName()
    {
        Should.Throw<ArgumentNullException>(() => new BatchState(null!, null!)).ParamName.ShouldBe("request");
        Should.Throw<ArgumentNullException>(() => new BatchState(CreateBatchRequest(), null!)).ParamName.ShouldBe("result");
    }

    /// <summary>Verifies a valid construction exposes the exact captured request and result, and cloning preserves both.</summary>
    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesExactCapturedValuesAndClones()
    {
        var request = CreateBatchRequest();
        var receipt = new BudgetLedgerReservationReceipt(
            new BudgetLedgerReservationReference(request.Scope, new BudgetReservationId(Guid.NewGuid())),
            request.OriginalRequests[0],
            new BudgetEffectiveReservation(DateTimeOffset.UnixEpoch));
        var result = new BudgetLedgerBatchReserved([receipt]);

        var state = new BatchState(request, result);

        state.Request.ShouldBeSameAs(request);
        state.Result.ShouldBeSameAs(result);
        var copy = state with { };
        copy.Request.ShouldBeSameAs(request);
        copy.Result.ShouldBeSameAs(result);
    }

    private static BudgetLedgerBatchReserveRequest CreateBatchRequest()
    {
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, null);
        var scope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), address);
        var request = new BudgetReservationRequest(
            scope.Id, new BudgetDimension("tokens"), 1, new BudgetUnit("count"), new OperationId(Guid.NewGuid()), null, new IdempotencyKey("state-test"));
        return new BudgetLedgerBatchReserveRequest(scope, [request]);
    }
}
