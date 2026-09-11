// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;

using AgentKit.TestSupport;

/// <summary>Verifies BudgetOverrunHoldResolved behavior and contracts.</summary>
public sealed class BudgetOverrunHoldResolvedTests
{
    [Fact]
    public void BudgetOverrunHoldResolved_WhenRevisionIsDefault_ThrowsExactParameterName()
    {
        var hold = HoldReference();
        Should.Throw<ArgumentOutOfRangeException>(() => new BudgetOverrunHoldResolved(hold, default, Receipt(hold))).ParamName.ShouldBe("resolutionRevision");
    }

    [Fact]
    public void BudgetOverrunHoldResolved_WhenReferencesAreNull_ThrowsExactParameterName()
    {
        var hold = HoldReference();
        Should.Throw<ArgumentNullException>(() => new BudgetOverrunHoldResolved(null!, new BudgetAccountingRevision(1), Receipt(hold))).ParamName.ShouldBe("hold");
        Should.Throw<ArgumentNullException>(() => new BudgetOverrunHoldResolved(hold, new BudgetAccountingRevision(1), null!)).ParamName.ShouldBe("enforcementReceipt");
    }

    [Fact]
    public void BudgetOverrunHoldResolved_WhenInputsAreValid_PreservesReceipt()
    {
        var hold = HoldReference();
        var receipt = Receipt(hold);
        var resolved = new BudgetOverrunHoldResolved(hold, new BudgetAccountingRevision(1), receipt);
        resolved.EnforcementReceipt.ShouldBeSameAs(receipt);
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
}
