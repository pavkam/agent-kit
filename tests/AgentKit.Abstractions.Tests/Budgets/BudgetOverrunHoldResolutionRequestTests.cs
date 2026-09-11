// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;

using AgentKit.TestSupport;

/// <summary>Verifies BudgetOverrunHoldResolutionRequest behavior and contracts.</summary>
public sealed class BudgetOverrunHoldResolutionRequestTests
{
    [Fact]
    public void BudgetOverrunHoldResolutionRequest_WhenHoldOrReceiptIsNull_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentNullException>(() => new BudgetOverrunHoldResolutionRequest(null!, null!, new IdempotencyKey("key"))).ParamName.ShouldBe("hold");
        Should.Throw<ArgumentNullException>(() => new BudgetOverrunHoldResolutionRequest(HoldReference(), null!, new IdempotencyKey("key"))).ParamName.ShouldBe("enforcementReceipt");
    }

    [Fact]
    public void BudgetOverrunHoldResolutionRequest_WhenReplayKeyIsDefault_ThrowsExactParameterName() => Should.Throw<ArgumentException>(() => new BudgetOverrunHoldResolutionRequest(HoldReference(), Receipt(HoldReference()), default)).ParamName.ShouldBe("idempotencyKey");

    private static BudgetOverrunHoldReference HoldReference()
    {
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.Parse("00000000-0000-0000-0000-000000000001")), null, null, null);
        var scope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.Parse("00000000-0000-0000-0000-000000000002")), address);
        return new BudgetOverrunHoldReference(scope, new BudgetLedgerReservationReference(scope, ReservationId()), new BudgetAccountingRevision(1));
    }

    private static BudgetReservationId ReservationId() => new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private static SecurityEnforcementIntentReceipt Receipt(BudgetOverrunHoldReference hold, SecurityOperationKind kind = SecurityOperationKind.StateMutation, SecurityEffect effect = SecurityEffect.Mutate, InputFingerprint? fingerprint = null, ImmutableArray<ProtectedResource> resources = default)
    {
        var identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("operator"), ExecutionSubjectKind.Human);
        var scope = new SecurityAuthorizationScope(hold.Boundary.Address.AgentId, null, new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("00000000-0000-0000-0000-000000000004")), null));
        var enforcement = new SecurityEnforcementRequest(scope, identity, new ComponentId("budget-operator"), kind, effect, resources.IsDefault ? [BudgetOverrunSecurityBinding.Resource(hold)] : resources, fingerprint ?? BudgetOverrunSecurityBinding.Fingerprint(hold), new SecurityRevocationVersion(1));
        return new SecurityEnforcementIntentReceipt(new SecurityEnforcementIntentId(Guid.Parse("00000000-0000-0000-0000-000000000005")), new GrantId(Guid.Parse("00000000-0000-0000-0000-000000000006")), new SecurityRequestId(Guid.Parse("00000000-0000-0000-0000-000000000007")), enforcement, null, new ContentHash("sha256:receipt"), DateTimeOffset.UnixEpoch);
    }
}
