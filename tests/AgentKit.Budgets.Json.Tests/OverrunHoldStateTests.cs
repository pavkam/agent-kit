// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

/// <summary>Verifies the internal mutable state retaining one boundary-specific overrun generation.</summary>
public sealed class OverrunHoldStateTests
{
    /// <summary>Verifies construction refuses a null creation-evidence argument.</summary>
    [Fact]
    public void Constructor_WhenEvidenceIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new OverrunHoldState(null!)).ParamName.ShouldBe("evidence");

    /// <summary>Verifies a freshly created generation is active until either terminal transition occurs.</summary>
    [Fact]
    public void IsActive_WhenFreshlyCreated_IsTrue()
    {
        var state = new OverrunHoldState(CreateEvidence());

        state.IsActive.ShouldBeTrue();
        state.AutomaticallyCleared.ShouldBeFalse();
        state.Resolution.ShouldBeNull();
    }

    /// <summary>Verifies automatic clearance makes the generation inactive.</summary>
    [Fact]
    public void IsActive_WhenAutomaticallyCleared_IsFalse()
    {
        var state = new OverrunHoldState(CreateEvidence()) { AutomaticallyCleared = true };

        state.IsActive.ShouldBeFalse();
    }

    /// <summary>Verifies an operator resolution makes the generation inactive.</summary>
    [Fact]
    public void IsActive_WhenResolved_IsFalse()
    {
        var evidence = CreateEvidence();
        var resolution = new BudgetOverrunHoldResolved(
            evidence.Reference, new BudgetAccountingRevision(2), CreateReceipt(evidence.Reference));
        var state = new OverrunHoldState(evidence) { Resolution = resolution };

        state.IsActive.ShouldBeFalse();
        state.Resolution.ShouldBeSameAs(resolution);
    }

    private static BudgetOverrunHold CreateEvidence()
    {
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, null);
        var boundary = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), address);
        var reservation = new BudgetLedgerReservationReference(boundary, new BudgetReservationId(Guid.NewGuid()));
        return new BudgetOverrunHold(
            new BudgetOverrunHoldReference(boundary, reservation, new BudgetAccountingRevision(1)),
            new BudgetDimension("tokens"),
            new BudgetUnit("count"),
            10,
            15,
            BudgetOverrunHoldPolicy.RequireAuthorizedResolution);
    }

    private static SecurityEnforcementIntentReceipt CreateReceipt(BudgetOverrunHoldReference hold)
    {
        var securityScope = new SecurityAuthorizationScope(
            hold.Boundary.Address.AgentId, null, new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null));
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
        return new SecurityEnforcementIntentReceipt(
            new SecurityEnforcementIntentId(Guid.NewGuid()),
            new GrantId(Guid.NewGuid()),
            new SecurityRequestId(Guid.NewGuid()),
            enforcement,
            null,
            new ContentHash("sha256:test"),
            DateTimeOffset.UnixEpoch);
    }
}
