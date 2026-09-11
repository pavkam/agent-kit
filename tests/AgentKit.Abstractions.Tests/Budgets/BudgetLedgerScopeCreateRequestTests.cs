// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetLedgerScopeCreateRequest behavior and contracts.</summary>
public sealed class BudgetLedgerScopeCreateRequestTests
{
    [Fact]
    public void BudgetLedgerScopeCreateRequest_WhenScopeLimitsAreValid_PreservesValidatedProductionEvidence()
    {
        var original = new BudgetScopeRequest(null, Address(), [Limit()], new IdempotencyKey("scope"));
        var request = new BudgetLedgerScopeCreateRequest(original, Admission());
        request.OriginalRequest.ShouldBeSameAs(original);
        request.OriginalRequest.Limits.ShouldBe(original.Limits);
    }

    [Fact]
    public void BudgetLedgerScopeCreateRequest_WhenOriginalReplayCoordinatesAreValid_PreservesEvidence()
    {
        var originalRequest = ScopeRequest();
        var admission = Admission();
        var request = Should.NotThrow(() => new BudgetLedgerScopeCreateRequest(originalRequest, admission));
        request.OriginalRequest.ShouldBe(originalRequest);
        request.Admission.ShouldBe(admission);
    }

    private static BudgetScopeAddress Address(OperationId? operationId = null) => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, operationId);
    private static BudgetScopeRequest ScopeRequest() => new(null, Address(), [], new IdempotencyKey("scope"));
    private static BudgetLimit Limit() => new(new BudgetDimension("tests.requests"), 1m, new BudgetUnit("requests"), BudgetLimitKind.Hard);
    private static BudgetScopeAdmission Admission() => new(1, 1, TimeSpan.FromMinutes(1));
}
