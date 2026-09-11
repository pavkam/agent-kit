// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;

using AgentKit.TestSupport;

/// <summary>Verifies BudgetOverrunSecurityBinding behavior and contracts.</summary>
public sealed class BudgetOverrunSecurityBindingTests
{
    [Fact]
    public void BudgetOverrunSecurityBinding_WhenHoldIsNull_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentNullException>(() => BudgetOverrunSecurityBinding.Fingerprint(null!)).ParamName.ShouldBe("hold");
        Should.Throw<ArgumentNullException>(() => BudgetOverrunSecurityBinding.Resource(null!)).ParamName.ShouldBe("hold");
    }

    [Fact]
    public void BudgetOverrunSecurityBinding_Matches_WhenReceiptUsesDifferentOperatorIdentity()
    {
        var hold = HoldReference();
        BudgetOverrunSecurityBinding.Matches(hold, Receipt(hold)).ShouldBeTrue();
    }

    [Fact]
    public void BudgetOverrunSecurityBinding_Matches_WhenReceiptFieldsDiffer_ReturnsFalse()
    {
        var hold = HoldReference();
        BudgetOverrunSecurityBinding.Matches(hold, Receipt(hold, kind: SecurityOperationKind.StateRead)).ShouldBeFalse();
        BudgetOverrunSecurityBinding.Matches(hold, Receipt(hold, effect: SecurityEffect.Observe)).ShouldBeFalse();
        BudgetOverrunSecurityBinding.Matches(hold, Receipt(hold, fingerprint: new InputFingerprint("sha256:other"))).ShouldBeFalse();
        BudgetOverrunSecurityBinding.Matches(hold, Receipt(hold, resources: [new ProtectedResource(ProtectedResourceKind.ApplicationState, "other")])).ShouldBeFalse();
        BudgetOverrunSecurityBinding.Matches(hold, Receipt(hold, resources: [BudgetOverrunSecurityBinding.Resource(hold), new ProtectedResource(ProtectedResourceKind.ApplicationState, "other")])).ShouldBeFalse();
    }

    [Fact]
    public void BudgetOverrunSecurityBinding_Matches_WhenArgumentIsNull_ThrowsExactParameterName()
    {
        var hold = HoldReference();
        Should.Throw<ArgumentNullException>(() => BudgetOverrunSecurityBinding.Matches(null!, Receipt(hold))).ParamName.ShouldBe("hold");
        Should.Throw<ArgumentNullException>(() => BudgetOverrunSecurityBinding.Matches(hold, null!)).ParamName.ShouldBe("receipt");
    }

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

    private static BudgetLedgerScopeReference Scope(OperationId? operationId = null) => new(new BudgetScopeId(Guid.NewGuid()), Address(operationId));
    [Fact]
    public void Resource_WhenCopiedTargetAddressChanges_ProducesDistinctCanonicalBinding()
    {
        var scope = Scope();
        var reservation = new BudgetLedgerReservationReference(scope, ReservationIdBudgetLedgerContracts());
        var original = new BudgetOverrunHoldReference(scope, reservation, new BudgetAccountingRevision(1));
        var changedAddress = new BudgetScopeAddress(scope.Address.TenantId, new PrincipalId("another-principal"), scope.Address.AgentId, scope.Address.SessionId, scope.Address.RunId, scope.Address.OperationId);
        var changedBoundary = new BudgetLedgerScopeReference(scope.Id, changedAddress);
        var changedReservationScope = new BudgetLedgerScopeReference(reservation.Scope.Id, changedAddress);
        var changedReservation = new BudgetLedgerReservationReference(changedReservationScope, reservation.Id);
        var changed = new BudgetOverrunHoldReference(changedBoundary, changedReservation, new BudgetAccountingRevision(1));
        BudgetOverrunSecurityBinding.Resource(changed).ShouldNotBe(BudgetOverrunSecurityBinding.Resource(original));
        BudgetOverrunSecurityBinding.Fingerprint(changed).ShouldNotBe(BudgetOverrunSecurityBinding.Fingerprint(original));
    }

    [Fact]
    public void Resource_WhenDelimiterLikeAddressTextDiffers_RemainsCollisionResistant()
    {
        var scopeId = new BudgetScopeId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var agentId = new AgentId(Guid.Parse("20000000-0000-0000-0000-000000000002"));
        var reservationId = new BudgetReservationId(Guid.Parse("30000000-0000-0000-0000-000000000003"));
        var leftScope = new BudgetLedgerScopeReference(scopeId, new BudgetScopeAddress(new TenantId("tenant:a/b"), new PrincipalId("principal:c"), agentId, null, null, null));
        var rightScope = new BudgetLedgerScopeReference(scopeId, new BudgetScopeAddress(new TenantId("tenant:a"), new PrincipalId("b/principal:c"), agentId, null, null, null));
        var left = new BudgetOverrunHoldReference(leftScope, new BudgetLedgerReservationReference(leftScope, reservationId), new BudgetAccountingRevision(1));
        var right = new BudgetOverrunHoldReference(rightScope, new BudgetLedgerReservationReference(rightScope, reservationId), new BudgetAccountingRevision(1));
        BudgetOverrunSecurityBinding.Resource(left).ShouldNotBe(BudgetOverrunSecurityBinding.Resource(right));
    }

    private static BudgetScopeAddress Address(OperationId? operationId = null) => new(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, operationId);
    private static BudgetReservationId ReservationIdBudgetLedgerContracts() => new(Guid.NewGuid());
}
