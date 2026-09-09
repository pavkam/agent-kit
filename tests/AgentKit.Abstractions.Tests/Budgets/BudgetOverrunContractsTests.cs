// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;

using AgentKit.TestSupport;

public sealed class BudgetOverrunContractsTests
{
    [Fact]
    public void BudgetOverrunHold_WhenRequiredReferenceIsNull_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentNullException>(() => new BudgetOverrunHold(null!, Dimension(), Unit(), 1, 2,
            BudgetOverrunHoldPolicy.ClearWhenReconciled)).ParamName.ShouldBe("reference");
    }

    [Fact]
    public void BudgetOverrunHold_WhenDimensionOrUnitIsDefault_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentNullException>(() => new BudgetOverrunHold(HoldReference(), default, Unit(), 1, 2, BudgetOverrunHoldPolicy.ClearWhenReconciled)).ParamName.ShouldBe("dimension");
        Should.Throw<ArgumentNullException>(() => new BudgetOverrunHold(HoldReference(), Dimension(), default, 1, 2, BudgetOverrunHoldPolicy.ClearWhenReconciled)).ParamName.ShouldBe("unit");
    }

    [Fact]
    public void BudgetLedgerBatchReserveHeld_WhenArrayIsInvalid_ThrowsAndValidEvidenceIsRetained()
    {
        Should.Throw<ArgumentException>(() => new BudgetLedgerBatchReserveHeld(default)).ParamName.ShouldBe("holds");
        Should.Throw<ArgumentException>(() => new BudgetLedgerBatchReserveHeld([])).ParamName.ShouldBe("holds");
        Should.Throw<ArgumentException>(() => new BudgetLedgerBatchReserveHeld([null!])).ParamName.ShouldBe("holds");
        _ = new BudgetLedgerBatchReserveHeld([Hold()]).Holds.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(-1, 1, "reserved")]
    [InlineData(1, -1, "currentActual")]
    public void BudgetOverrunHold_WhenAmountIsNegative_ThrowsExactParameterName(decimal reserved, decimal actual, string name)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new BudgetOverrunHold(HoldReference(), Dimension(), Unit(), reserved,
            actual, BudgetOverrunHoldPolicy.ClearWhenReconciled)).ParamName.ShouldBe(name);
    }

    [Fact]
    public void BudgetOverrunHold_WhenPolicyIsUndefined_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new BudgetOverrunHold(HoldReference(), Dimension(), Unit(), 1, 2,
            (BudgetOverrunHoldPolicy) int.MaxValue)).ParamName.ShouldBe("policy");
    }

    [Fact]
    public void BudgetCommitResult_WhenPresentAccountingRevisionIsDefault_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new BudgetCommitResult(ReservationId(), 1, 1, 0, 0,
            default(BudgetAccountingRevision), [])).ParamName.ShouldBe("accountingRevision");
    }

    [Fact]
    public void BudgetCorrectionResult_WhenPresentAccountingRevisionIsDefault_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new BudgetCorrectionResult(ReservationId(), 1, 1, 1,
            default(BudgetAccountingRevision), [], [])).ParamName.ShouldBe("accountingRevision");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BudgetCommitResult_WhenHoldArrayIsDefault_ThrowsExactParameterName(bool presentRevision)
    {
        BudgetAccountingRevision? revision = presentRevision ? new BudgetAccountingRevision(1) : null;
        Should.Throw<ArgumentException>(() => new BudgetCommitResult(ReservationId(), 1, 1, 0, 0, revision,
            default)).ParamName.ShouldBe("createdOverrunHolds");
    }

    [Fact]
    public void BudgetSnapshot_WhenActiveHoldArrayIsDefault_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentException>(() => new BudgetSnapshot(new BudgetScopeId(Guid.NewGuid()), DateTimeOffset.UnixEpoch,
            [], default)).ParamName.ShouldBe("activeOverrunHolds");
    }

    [Fact]
    public void OverrunResultArrays_WhenDefaultOrContainingNull_ThrowExactParameterName()
    {
        Should.Throw<ArgumentException>(() => new BudgetCommitResult(ReservationId(), 1, 1, 0, 0, null, [null!]))
            .ParamName.ShouldBe("createdOverrunHolds");
        Should.Throw<ArgumentException>(() => new BudgetCorrectionResult(ReservationId(), 1, 1, 1, null, default, []))
            .ParamName.ShouldBe("createdOverrunHolds");
        Should.Throw<ArgumentException>(() => new BudgetCorrectionResult(ReservationId(), 1, 1, 1, null, [null!], []))
            .ParamName.ShouldBe("createdOverrunHolds");
        Should.Throw<ArgumentException>(() => new BudgetCorrectionResult(ReservationId(), 1, 1, 1, null, [], default))
            .ParamName.ShouldBe("clearedOverrunHolds");
        Should.Throw<ArgumentException>(() => new BudgetCorrectionResult(ReservationId(), 1, 1, 1, null, [], [null!]))
            .ParamName.ShouldBe("clearedOverrunHolds");
        Should.Throw<ArgumentException>(() => new BudgetSnapshot(new BudgetScopeId(Guid.NewGuid()), DateTimeOffset.UnixEpoch, [], [null!]))
            .ParamName.ShouldBe("activeOverrunHolds");
        Should.Throw<ArgumentException>(() => new BudgetOverrunHoldResolutionBlocked(HoldReference(), [null!], []))
            .ParamName.ShouldBe("currentOverruns");
        Should.Throw<ArgumentException>(() => new BudgetOverrunHoldResolutionBlocked(HoldReference(), [], [null!]))
            .ParamName.ShouldBe("hardLimitFailures");
    }

    [Fact]
    public void OverrunResultArrays_WhenEmptyIsAllowed_PreserveEmptyEvidence()
    {
        new BudgetCommitResult(ReservationId(), 1, 1, 0, 0, null, []).CreatedOverrunHolds.ShouldBeEmpty();
        var result = new BudgetCorrectionResult(ReservationId(), 1, 1, 1, null, [], []);
        result.CreatedOverrunHolds.ShouldBeEmpty();
        result.ClearedOverrunHolds.ShouldBeEmpty();
        new BudgetSnapshot(new BudgetScopeId(Guid.NewGuid()), DateTimeOffset.UnixEpoch, [], []).ActiveOverrunHolds.ShouldBeEmpty();
    }

    [Fact]
    public void BudgetOverrunHoldResolutionRequest_WhenHoldOrReceiptIsNull_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentNullException>(() => new BudgetOverrunHoldResolutionRequest(null!, null!, new IdempotencyKey("key")))
            .ParamName.ShouldBe("hold");
        Should.Throw<ArgumentNullException>(() => new BudgetOverrunHoldResolutionRequest(HoldReference(), null!, new IdempotencyKey("key")))
            .ParamName.ShouldBe("enforcementReceipt");
    }

    [Fact]
    public void BudgetOverrunHoldResolutionRequest_WhenReplayKeyIsDefault_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentException>(() => new BudgetOverrunHoldResolutionRequest(HoldReference(), Receipt(HoldReference()),
            default)).ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void BudgetOverrunHoldResolved_WhenRevisionIsDefault_ThrowsExactParameterName()
    {
        var hold = HoldReference();
        Should.Throw<ArgumentOutOfRangeException>(() => new BudgetOverrunHoldResolved(hold, default, Receipt(hold)))
            .ParamName.ShouldBe("resolutionRevision");
    }

    [Fact]
    public void BudgetOverrunHoldResolved_WhenReferencesAreNull_ThrowsExactParameterName()
    {
        var hold = HoldReference();
        Should.Throw<ArgumentNullException>(() => new BudgetOverrunHoldResolved(null!, new BudgetAccountingRevision(1), Receipt(hold))).ParamName.ShouldBe("hold");
        Should.Throw<ArgumentNullException>(() => new BudgetOverrunHoldResolved(hold, new BudgetAccountingRevision(1), null!)).ParamName.ShouldBe("enforcementReceipt");
    }

    [Fact]
    public void BudgetOverrunHoldResolutionBlocked_WhenBothArraysAreEmpty_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentException>(() => new BudgetOverrunHoldResolutionBlocked(HoldReference(), [], []))
            .ParamName.ShouldBe("currentOverruns");
    }

    [Fact]
    public void BudgetOverrunHoldResolutionBlocked_WhenHoldOrArraysAreInvalid_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentNullException>(() => new BudgetOverrunHoldResolutionBlocked(null!, [Hold()], [])).ParamName.ShouldBe("hold");
        Should.Throw<ArgumentException>(() => new BudgetOverrunHoldResolutionBlocked(HoldReference(), default, [])).ParamName.ShouldBe("currentOverruns");
        Should.Throw<ArgumentException>(() => new BudgetOverrunHoldResolutionBlocked(HoldReference(), [], default)).ParamName.ShouldBe("hardLimitFailures");
    }

    [Fact]
    public void BudgetOverrunHoldResolutionBlocked_WhenOneBlockerClassIsPresent_PreservesIt()
    {
        var hold = HoldReference();
        _ = new BudgetOverrunHoldResolutionBlocked(hold, [Hold()], []).CurrentOverruns.ShouldHaveSingleItem();
        _ = new BudgetOverrunHoldResolutionBlocked(hold, [], [LimitFailure()]).HardLimitFailures.ShouldHaveSingleItem();
    }

    [Fact]
    public void BudgetOverrunHoldResolved_WhenInputsAreValid_PreservesReceipt()
    {
        var hold = HoldReference();
        var receipt = Receipt(hold);
        var resolved = new BudgetOverrunHoldResolved(hold, new BudgetAccountingRevision(1), receipt);
        resolved.EnforcementReceipt.ShouldBeSameAs(receipt);
    }

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

    [Fact]
    public void ThrowIfNoBudgetOverrunResolutionBlockers_WhenEvidenceExists_DoesNotThrow()
    {
        ArgumentException.ThrowIfNoBudgetOverrunResolutionBlockers([Hold()], []);
        ArgumentException.ThrowIfNoBudgetOverrunResolutionBlockers([], [LimitFailure()]);
    }

    [Fact]
    public void ThrowIfNoBudgetOverrunResolutionBlockers_WhenEmpty_UsesExplicitParameterName()
    {
        Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNoBudgetOverrunResolutionBlockers([], [], "blockers"))
            .ParamName.ShouldBe("blockers");
    }

    [Fact]
    public void ThrowIfNoBudgetOverrunResolutionBlockers_WhenArrayIsDefault_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNoBudgetOverrunResolutionBlockers(default, [])).ParamName.ShouldBe("currentOverruns");
        Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNoBudgetOverrunResolutionBlockers([], default)).ParamName.ShouldBe("hardLimitFailures");
    }

    [Fact]
    public void ThrowIfNotBudgetScopeAncestorAddress_WhenBoundaryNarrowsAddress_DoesNotThrow()
    {
        var tenant = new TenantId("tenant");
        var principal = new PrincipalId("principal");
        var agent = new AgentId(Guid.NewGuid());
        var charged = new BudgetScopeAddress(tenant, principal, agent, new SessionId(Guid.NewGuid()), new RunId(Guid.NewGuid()), new OperationId(Guid.NewGuid()));
        ArgumentException.ThrowIfNotBudgetScopeAncestorAddress(new BudgetScopeAddress(tenant, principal, agent, null, null, null), charged);
    }

    [Fact]
    public void ThrowIfNotBudgetScopeAncestorAddress_WhenMismatch_UsesExplicitParameterName()
    {
        var agent = new AgentId(Guid.NewGuid());
        var boundary = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), agent, null, null, null);
        var charged = new BudgetScopeAddress(new TenantId("other"), new PrincipalId("principal"), agent, null, null, null);

        Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNotBudgetScopeAncestorAddress(boundary, charged, "chargedScope"))
            .ParamName.ShouldBe("chargedScope");
    }

    [Fact]
    public void ThrowIfNotBudgetScopeAncestorAddress_WhenNullOrInferredMismatch_UsesExactParameterName()
    {
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, null);
        Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfNotBudgetScopeAncestorAddress(null!, address)).ParamName.ShouldBe("boundary");
        Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfNotBudgetScopeAncestorAddress(address, null!)).ParamName.ShouldBe("charged");
        var changed = new BudgetScopeAddress(new TenantId("other"), address.PrincipalId, address.AgentId, null, null, null);
        Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNotBudgetScopeAncestorAddress(address, changed)).ParamName.ShouldBe("changed");
    }

    private static BudgetOverrunHoldReference HoldReference()
    {
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.Parse("00000000-0000-0000-0000-000000000001")), null, null, null);
        var scope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.Parse("00000000-0000-0000-0000-000000000002")), address);
        return new BudgetOverrunHoldReference(scope, new BudgetLedgerReservationReference(scope, ReservationId()), new BudgetAccountingRevision(1));
    }

    private static BudgetReservationId ReservationId() => new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private static BudgetDimension Dimension() => new("tests.overrun");
    private static BudgetUnit Unit() => new("count");
    private static BudgetOverrunHold Hold() => new(HoldReference(), Dimension(), Unit(), 1, 2, BudgetOverrunHoldPolicy.ClearWhenReconciled);
    private static BudgetLimitFailure LimitFailure() => new(HoldReference().Boundary.Id, Dimension(), BudgetLimitKind.Hard, 10, 11, 0, Unit(), "limit");

    private static SecurityEnforcementIntentReceipt Receipt(BudgetOverrunHoldReference hold,
        SecurityOperationKind kind = SecurityOperationKind.StateMutation,
        SecurityEffect effect = SecurityEffect.Mutate, InputFingerprint? fingerprint = null,
        ImmutableArray<ProtectedResource> resources = default)
    {
        var identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("operator"), ExecutionSubjectKind.Human);
        var scope = new SecurityAuthorizationScope(hold.Boundary.Address.AgentId, null,
            new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("00000000-0000-0000-0000-000000000004")), null));
        var enforcement = new SecurityEnforcementRequest(scope, identity, new ComponentId("budget-operator"),
            kind, effect, resources.IsDefault ? [BudgetOverrunSecurityBinding.Resource(hold)] : resources,
            fingerprint ?? BudgetOverrunSecurityBinding.Fingerprint(hold), new SecurityRevocationVersion(1));
        return new SecurityEnforcementIntentReceipt(new SecurityEnforcementIntentId(Guid.Parse("00000000-0000-0000-0000-000000000005")),
            new GrantId(Guid.Parse("00000000-0000-0000-0000-000000000006")), new SecurityRequestId(Guid.Parse("00000000-0000-0000-0000-000000000007")),
            enforcement, null, new ContentHash("sha256:receipt"), DateTimeOffset.UnixEpoch);
    }
}
