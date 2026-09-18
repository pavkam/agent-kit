// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

/// <summary>Verifies the internal replay binding for one operator overrun-resolution replay key.</summary>
public sealed class OverrunResolutionStateTests
{
    /// <summary>Verifies construction refuses either null argument, attributing the exact parameter name.</summary>
    [Fact]
    public void Constructor_WhenArgumentIsNull_ThrowsWithExactParameterName()
    {
        var request = CreateRequest();

        Should.Throw<ArgumentNullException>(() => new OverrunResolutionState(null!, null!)).ParamName.ShouldBe("request");
        Should.Throw<ArgumentNullException>(() => new OverrunResolutionState(request, null!)).ParamName.ShouldBe("result");
    }

    /// <summary>Verifies a valid construction exposes the exact captured request and result.</summary>
    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesExactCapturedValues()
    {
        var request = CreateRequest();
        var hardFailure = new BudgetLimitFailure(
            request.Hold.Boundary.Id,
            new BudgetDimension("tokens"),
            BudgetLimitKind.Hard,
            10,
            BudgetQuantity.FromDecimal(12),
            default,
            new BudgetUnit("count"),
            "Current accounting exceeds the captured hard boundary.");
        var result = new BudgetOverrunHoldResolutionBlocked(request.Hold, [], [hardFailure]);

        var state = new OverrunResolutionState(request, result);

        state.Request.ShouldBeSameAs(request);
        state.Result.ShouldBeSameAs(result);
    }

    private static BudgetOverrunHoldResolutionRequest CreateRequest()
    {
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, null);
        var boundary = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), address);
        var reservation = new BudgetLedgerReservationReference(boundary, new BudgetReservationId(Guid.NewGuid()));
        var hold = new BudgetOverrunHoldReference(boundary, reservation, new BudgetAccountingRevision(1));
        var securityScope = new SecurityAuthorizationScope(
            address.AgentId, null, new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null));
        var identity = TestSupport.TestExecutionIdentity.Create(
            new TenantId("tenant"), new PrincipalId("operator"), ExecutionSubjectKind.Human);
        var enforcement = new SecurityEnforcementRequest(
            securityScope,
            identity,
            new ComponentId("budget-operator"),
            SecurityOperationKind.StateMutation,
            SecurityEffect.Mutate,
            [BudgetOverrunSecurityBinding.Resource(hold)],
            BudgetOverrunSecurityBinding.Fingerprint(hold),
            new SecurityRevocationVersion(1));
        var receipt = new SecurityEnforcementIntentReceipt(
            new SecurityEnforcementIntentId(Guid.NewGuid()),
            new GrantId(Guid.NewGuid()),
            new SecurityRequestId(Guid.NewGuid()),
            enforcement,
            null,
            new ContentHash("sha256:test"),
            DateTimeOffset.UnixEpoch);
        return new BudgetOverrunHoldResolutionRequest(hold, receipt, new IdempotencyKey("resolution-state-key"));
    }
}
