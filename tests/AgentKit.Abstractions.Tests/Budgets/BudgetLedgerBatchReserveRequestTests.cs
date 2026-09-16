// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetLedgerBatchReserveRequest behavior and contracts.</summary>
public sealed class BudgetLedgerBatchReserveRequestTests
{
    [Fact]
    public void BudgetLedgerBatchReserveRequest_WhenOriginalBatchIsValid_PreservesOnlyOriginalEvidence()
    {
        var scope = Scope();
        var requests = Requests(scope.Id);
        var request = new BudgetLedgerBatchReserveRequest(scope, requests);
        request.Scope.ShouldBe(scope);
        request.OriginalRequests.ShouldBe(requests);
        typeof(BudgetLedgerBatchReserveRequest).GetProperty(nameof(BudgetLedgerBatchReserveRequest.OriginalRequests))!.SetMethod.ShouldBeNull();
    }

    [Fact]
    public void BudgetLedgerBatchReserveRequest_WhenCopiedRequestIsInvalid_RejectsBeforePropertyAssignment()
    {
        var scope = Scope();
        var requests = Requests(scope.Id);
        requests = requests.SetItem(0, requests[0] with { Amount = 0m });
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetLedgerBatchReserveRequest(scope, requests));
        exception.ParamName.ShouldBe("originalRequests");
    }

    [Fact]
    public void BudgetLedgerBatchReserveRequest_WhenScopeBindsAnotherOperation_RejectsBeforePropertyAssignment()
    {
        var scopeOperation = OperationId();
        var scope = Scope(scopeOperation);
        var requestOperation = OperationId();
        ImmutableArray<BudgetReservationRequest> requests = [Request(scope.Id, operationId: requestOperation), Request(scope.Id, amount: 2m, idempotencyKey: "second", operationId: requestOperation)];
        var exception = Should.Throw<ArgumentException>(() => new BudgetLedgerBatchReserveRequest(scope, requests));
        exception.ParamName.ShouldBe("originalRequests");
    }

    [Fact]
    public void BudgetLedgerBatchReserveRequest_WhenScopeBindsBatchOperation_AcceptsOriginalEvidence()
    {
        var operationId = OperationId();
        var scope = Scope(operationId);
        ImmutableArray<BudgetReservationRequest> requests = [Request(scope.Id, operationId: operationId), Request(scope.Id, amount: 2m, idempotencyKey: "second", operationId: operationId)];
        var request = Should.NotThrow(() => new BudgetLedgerBatchReserveRequest(scope, requests));
        request.OriginalRequests.ShouldBe(requests);
    }

    [Fact]
    public void BudgetLedgerBatchReserveRequest_WhenEveryEqualityFieldMatches_HasEqualHashCode()
    {
        var scope = Scope();
        var requests = Requests(scope.Id);
        var first = new BudgetLedgerBatchReserveRequest(scope, requests);
        var second = new BudgetLedgerBatchReserveRequest(scope, requests);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void BudgetLedgerBatchReserveRequest_WhenScopeOrOrderedOriginalRequestsDiffer_IsNotEqual()
    {
        var scope = Scope();
        var requests = Requests(scope.Id);
        var first = new BudgetLedgerBatchReserveRequest(scope, requests);
        var otherScope = Scope();
        var differentScope = new BudgetLedgerBatchReserveRequest(otherScope, Requests(otherScope.Id));
        var differentRequests = new BudgetLedgerBatchReserveRequest(scope, [.. requests.Reverse()]);
        first.ShouldNotBe(differentScope);
        first.ShouldNotBe(differentRequests);
    }

    private static BudgetLedgerScopeReference Scope(OperationId? operationId = null) => new(new BudgetScopeId(Guid.NewGuid()), Address(operationId));
    private static BudgetScopeAddress Address(OperationId? operationId = null) => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, operationId);
    private static OperationId OperationId() => new(Guid.NewGuid());
    private static BudgetReservationRequest Request(BudgetScopeId scopeId, decimal amount = 1m, string dimension = "tests.requests", string unit = "requests", string idempotencyKey = "key", OperationId? operationId = null) => new(scopeId, new BudgetDimension(dimension), amount, new BudgetUnit(unit), operationId ?? new OperationId(Guid.Parse("00000000-0000-0000-0000-000000000101")), null, new IdempotencyKey(idempotencyKey));
    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var scope = Scope();
        var original = new BudgetLedgerBatchReserveRequest(scope, Requests(scope.Id));
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ImmutableArray<BudgetReservationRequest> Requests(BudgetScopeId scopeId) => [Request(scopeId, idempotencyKey: "one"), Request(scopeId, amount: 2m, dimension: "tests.tokens", unit: "tokens", idempotencyKey: "two")];
}
