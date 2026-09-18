// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

/// <summary>Verifies the internal mutable in-memory projection of one replayed reservation.</summary>
public sealed class ReservationStateTests
{
    /// <summary>Verifies construction refuses a null receipt.</summary>
    [Fact]
    public void Constructor_WhenReceiptIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ReservationState(null!, CreateLineage(), BudgetAggregationKind.Sum))
            .ParamName.ShouldBe("receipt");

    /// <summary>Verifies construction refuses a default or empty lineage.</summary>
    [Fact]
    public void Constructor_WhenLineageIsDefaultOrEmpty_ThrowsArgumentException()
    {
        var receipt = CreateReceipt();

        Should.Throw<ArgumentException>(() => new ReservationState(receipt, default, BudgetAggregationKind.Sum))
            .ParamName.ShouldBe("lineage");
        Should.Throw<ArgumentException>(() => new ReservationState(receipt, [], BudgetAggregationKind.Sum))
            .ParamName.ShouldBe("lineage");
    }

    /// <summary>Verifies construction refuses an undefined aggregation kind.</summary>
    [Fact]
    public void Constructor_WhenAggregationIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(
                () => new ReservationState(CreateReceipt(), CreateLineage(), (BudgetAggregationKind) 9_999))
            .ParamName.ShouldBe("aggregation");

    /// <summary>Verifies a valid construction exposes the exact captured receipt, lineage, and aggregation, with unstarted defaults.</summary>
    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesExactCapturedValuesAndUnstartedDefaults()
    {
        var receipt = CreateReceipt();
        var lineage = CreateLineage();

        var state = new ReservationState(receipt, lineage, BudgetAggregationKind.Sum);

        state.Receipt.ShouldBeSameAs(receipt);
        state.Lineage.ShouldBe(lineage);
        state.Aggregation.ShouldBe(BudgetAggregationKind.Sum);
        state.StartedAt.ShouldBeNull();
        state.Released.ShouldBeFalse();
        state.Commit.ShouldBeNull();
        state.OriginalCommit.ShouldBeNull();
        state.AccountingRevision.ShouldBeNull();
        state.StartExpiration.ShouldBeNull();
        state.LatestCorrectionRevision.ShouldBe(0);
        state.Corrections.ShouldBeEmpty();
        state.Reconciliations.ShouldBeEmpty();
        state.IsCapacityRetaining.ShouldBeTrue();
    }

    /// <summary>Verifies a released reservation no longer retains capacity.</summary>
    [Fact]
    public void IsCapacityRetaining_WhenReleased_IsFalse()
    {
        var state = new ReservationState(CreateReceipt(), CreateLineage(), BudgetAggregationKind.Sum) { Released = true };

        state.IsCapacityRetaining.ShouldBeFalse();
    }

    /// <summary>Verifies a settled reservation no longer retains capacity.</summary>
    [Fact]
    public void IsCapacityRetaining_WhenSettled_IsFalse()
    {
        var state = new ReservationState(CreateReceipt(), CreateLineage(), BudgetAggregationKind.Sum)
        {
            Commit = new BudgetCommitResult(
                new BudgetReservationId(Guid.NewGuid()), 5, 5, 0, 0, new BudgetAccountingRevision(1), []),
        };

        state.IsCapacityRetaining.ShouldBeFalse();
    }

    private static BudgetLedgerReservationReceipt CreateReceipt()
    {
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, null);
        var scope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), address);
        var request = new BudgetReservationRequest(
            scope.Id, new BudgetDimension("tokens"), 1, new BudgetUnit("count"), new OperationId(Guid.NewGuid()), null, new IdempotencyKey("reservation-state"));
        return new BudgetLedgerReservationReceipt(
            new BudgetLedgerReservationReference(scope, new BudgetReservationId(Guid.NewGuid())),
            request,
            new BudgetEffectiveReservation(DateTimeOffset.UnixEpoch));
    }

    private static ImmutableArray<ScopeState> CreateLineage()
    {
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, null);
        var reference = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), address);
        var request = new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(null, address, [], new IdempotencyKey($"lineage-{Guid.NewGuid():N}")),
            new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5), BudgetOverrunHoldPolicy.ClearWhenReconciled));
        return [new ScopeState(reference, request, null, 1)];
    }
}
