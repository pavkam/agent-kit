// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Defines portable replay, atomicity, lifecycle, masking, and recovery behavior for <see cref="IBudgetLedger"/>.</summary>
/// <typeparam name="TFixture">The implementation-specific isolated fixture.</typeparam>
public abstract class BudgetLedgerConformanceTests<TFixture>
    where TFixture : IBudgetLedgerConformanceFixture, new()
{
    /// <inheritdoc/>
    [Fact]
    public void Descriptor_WhenRead_IsStableAndMatchesDeclaredCapabilities()
    {
        var fixture = new TFixture();
        var ledger = fixture.CreateLedger();

        var first = ledger.Descriptor;
        var second = ledger.Descriptor;

        _ = first.ShouldNotBeNull();
        first.ShouldBe(fixture.ExpectedDescriptor);
        second.ShouldBe(first);
    }
    private static readonly BudgetDimension Dimension = new("test.sum");
    private static readonly BudgetDimension MaximumDimension = new("test.maximum");
    private static readonly BudgetDimension GaugeDimension = new("test.gauge");
    private static readonly BudgetDimension DurationDimension = new("test.duration");
    private static readonly BudgetDimension UnlimitedDimension = new("test.unlimited");
    private static readonly BudgetDimension MultiUnitDimension = new("test.multi-unit");
    private static readonly BudgetUnit Count = new("count");
    private static readonly BudgetUnit Seconds = new("seconds");
    private static readonly BudgetUnit Bytes = new("bytes");
    private static readonly AgentId Agent = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));

    /// <summary>Verifies cancellation raised by the identity collaborator prevents scope insertion and replay binding.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenIdentityGenerationCancels_DoesNotCreateScope()
    {
        var fixture = new TFixture();
        var ledger = fixture.CreateLedger();
        using var source = new CancellationTokenSource();
        fixture.ArmScopeIdCancellation(source);
        var original = new BudgetScopeRequest(null, Address("tenant"), [new BudgetLimit(Dimension, 10, Count, BudgetLimitKind.Hard)], new IdempotencyKey("cancelled-scope"));
        var request = new BudgetLedgerScopeCreateRequest(original, new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5)));

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await ledger.CreateScopeAsync(request, source.Token));

        _ = (await ledger.CreateScopeAsync(request)).ShouldBeOfType<BudgetLedgerScopeCreated>();
    }

    /// <summary>Verifies exact replay resolves before clock, descriptor, or identity collaborators are invoked.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenExactReplay_DoesNotInvokeAdmissionCollaborators()
    {
        var fixture = new TFixture();
        var ledger = fixture.CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-replay-order", 10);
        var request = new BudgetLedgerBatchReserveRequest(scope, [Reservation(scope, "item", 2)]);
        var original = await ledger.ReserveBatchAsync(request);
        fixture.ArmClockFailure();
        fixture.ArmCatalogFailure();
        fixture.ArmSecondReservationIdFailure();

        var replay = await ledger.ReserveBatchAsync(request);

        replay.ShouldBe(original);
    }

    /// <summary>Verifies failure while generating a later identity leaves no partial reservation.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenSecondIdentityFails_LeavesBatchUnreserved()
    {
        var fixture = new TFixture();
        var ledger = fixture.CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-id-failure", 10);
        fixture.ArmSecondReservationIdFailure();
        var request = new BudgetLedgerBatchReserveRequest(scope, [Reservation(scope, "a", 2), Reservation(scope, "b", 3)]);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await ledger.ReserveBatchAsync(request));

        (await ledger.GetSnapshotAsync(scope)).Usages.Single().Reserved.ToDecimalChecked().ShouldBe(0);
    }

    /// <summary>Verifies cancellation raised by a collaborator before commit leaves no partial reservation.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenCollaboratorCancelsBeforeCommit_LeavesBatchUnreserved()
    {
        var fixture = new TFixture();
        var ledger = fixture.CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-cancel", 10);
        using var source = new CancellationTokenSource();
        fixture.ArmSecondReservationIdCancellation(source);
        var request = new BudgetLedgerBatchReserveRequest(scope, [Reservation(scope, "a", 2), Reservation(scope, "b", 3)]);

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await ledger.ReserveBatchAsync(request, source.Token));

        (await ledger.GetSnapshotAsync(scope)).Usages.Single().Reserved.ToDecimalChecked().ShouldBe(0);
    }

    /// <summary>Verifies exact admission arithmetic rejects an oversized batch without decimal overflow or partial reservation.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenArithmeticOverflows_LeavesBatchUnreserved()
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-overflow", decimal.MaxValue);
        var request = new BudgetLedgerBatchReserveRequest(scope, [Reservation(scope, "a", decimal.MaxValue), Reservation(scope, "b", 1)]);

        var rejection = (await ledger.ReserveBatchAsync(request)).ShouldBeOfType<BudgetLedgerBatchReserveRejected>();

        (await ledger.GetSnapshotAsync(scope)).Usages.Single().Reserved.ToDecimalChecked().ShouldBe(0);
        rejection.Failure.RequestedAmount.ShouldBe(
            BudgetQuantity.FromDecimal(decimal.MaxValue).Add(BudgetQuantity.FromDecimal(1)));
    }

    /// <summary>Verifies descriptor failure cannot sweep a separate expired reservation before returning.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenCatalogFails_DoesNotReleaseUnrelatedExpiredReservation()
    {
        var fixture = new TFixture();
        var ledger = fixture.CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-catalog-failure", 10);
        var expiring = Reservation(scope, "old", 1) with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(1) };
        var receipt = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [expiring]))).ShouldBeOfType<BudgetLedgerBatchReserved>().Receipts[0];
        fixture.Advance(TimeSpan.FromMinutes(10));
        fixture.ArmCatalogFailure();

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [Reservation(scope, "new", 1)])));

        _ = (await ledger.MarkStartedAsync(receipt.Reservation)).ShouldBeOfType<BudgetStartExpired>();
    }

    /// <summary>Verifies a failed snapshot clock read cannot release an expired reservation as a hidden side effect.</summary>
    [Fact]
    public async Task GetSnapshotAsync_WhenClockFails_DoesNotReleaseExpiredReservation()
    {
        var fixture = new TFixture();
        var ledger = fixture.CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-clock-failure", 10);
        var expiring = Reservation(scope, "old", 1) with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(1) };
        var receipt = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [expiring]))).ShouldBeOfType<BudgetLedgerBatchReserved>().Receipts[0];
        fixture.Advance(TimeSpan.FromMinutes(10));
        fixture.ArmClockFailure();

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await ledger.GetSnapshotAsync(scope));

        _ = (await ledger.MarkStartedAsync(receipt.Reservation)).ShouldBeOfType<BudgetStartExpired>();
    }

    /// <summary>Verifies maximum accounting tracks the largest settled value and recomputes after corrections.</summary>
    [Fact]
    public async Task GetSnapshotAsync_WhenMaximumValuesSettleAndCorrect_ReportsCurrentPeak()
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-maximum", 10, MaximumDimension);
        var first = await ReserveOneAsync(ledger, scope, "max-a", 6, MaximumDimension, Count);
        _ = await ledger.MarkStartedAsync(first);
        _ = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(first, 6));
        var second = await ReserveOneAsync(ledger, scope, "max-b", 4, MaximumDimension, Count);
        _ = await ledger.MarkStartedAsync(second);
        _ = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(second, 9));

        (await ledger.GetSnapshotAsync(scope)).Usages.Single().Committed.ToDecimalChecked().ShouldBe(9);
        _ = await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(second, 3, 1));
        (await ledger.GetSnapshotAsync(scope)).Usages.Single().Committed.ToDecimalChecked().ShouldBe(6);
        _ = await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(first, 2, 1));
        (await ledger.GetSnapshotAsync(scope)).Usages.Single().Committed.ToDecimalChecked().ShouldBe(3);
    }

    /// <summary>Verifies a concurrency gauge remains occupied while started usage is unknown and releases only with proof.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenGaugeIsUnresolved_BlocksUntilProvenReleased()
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-gauge", 3, GaugeDimension);
        var first = await ReserveOneAsync(ledger, scope, "gauge-a", 3, GaugeDimension, Count);
        _ = await ledger.MarkStartedAsync(first);
        _ = (await ledger.ReleaseUnstartedAsync(first)).ShouldBeOfType<BudgetLedgerRetainedStarted>();
        (await ledger.GetSnapshotAsync(scope)).Usages.Single().Reserved.ToDecimalChecked().ShouldBe(3);

        _ = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [Reservation(scope, "gauge-b", 1, GaugeDimension, Count)]))).ShouldBeOfType<BudgetLedgerBatchReserveRejected>();
        _ = (await ledger.ReconcileAsync(new BudgetLedgerReconciliationRequest(first, new BudgetStillUnknown(), new IdempotencyKey("gauge-unknown")))).ShouldBeOfType<BudgetLedgerReconciliationRetainedUnknown>();
        _ = (await ledger.ReconcileAsync(new BudgetLedgerReconciliationRequest(first, new BudgetNoUsageProven(), new IdempotencyKey("gauge-proof")))).ShouldBeOfType<BudgetLedgerReconciliationReleased>();
        _ = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [Reservation(scope, "gauge-b", 3, GaugeDimension, Count)]))).ShouldBeOfType<BudgetLedgerBatchReserved>();
    }

    /// <summary>Verifies combined and batched gauge amounts consume identical live capacity and settlement releases current occupancy.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenGaugeAmountsAreSplit_PreservesCapacityUnits()
    {
        var ledger = new TFixture().CreateLedger();
        var combinedScope = await CreateScopeAsync(ledger, "scope-gauge-combined", 3, GaugeDimension);
        var combined = await ReserveOneAsync(ledger, combinedScope, "gauge-combined", 3, GaugeDimension, Count);
        _ = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(combinedScope, [Reservation(combinedScope, "gauge-combined-extra", 1, GaugeDimension, Count)]))).ShouldBeOfType<BudgetLedgerBatchReserveRejected>();
        _ = await ledger.MarkStartedAsync(combined);
        var partial = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(combined, 1));
        partial.Released.ShouldBe(2);
        var combinedUsage = (await ledger.GetSnapshotAsync(combinedScope)).Usages.Single();
        combinedUsage.Reserved.ToDecimalChecked().ShouldBe(0);
        combinedUsage.Committed.ToDecimalChecked().ShouldBe(0);

        var splitScope = await CreateScopeAsync(ledger, "scope-gauge-split", 3, GaugeDimension);
        _ = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(splitScope,
            [Reservation(splitScope, "gauge-one", 1, GaugeDimension, Count), Reservation(splitScope, "gauge-two", 1, GaugeDimension, Count), Reservation(splitScope, "gauge-three", 1, GaugeDimension, Count)]))).ShouldBeOfType<BudgetLedgerBatchReserved>();
        _ = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(splitScope, [Reservation(splitScope, "gauge-split-extra", 1, GaugeDimension, Count)]))).ShouldBeOfType<BudgetLedgerBatchReserveRejected>();
        (await ledger.GetSnapshotAsync(splitScope)).Usages.Single().Reserved.ToDecimalChecked().ShouldBe(3);
    }

    /// <summary>Verifies duration dimensions accumulate settled elapsed quantities using their declared unit.</summary>
    [Fact]
    public async Task GetSnapshotAsync_WhenDurationsSettle_AccumulatesElapsedUsage()
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-duration", 20, DurationDimension, Seconds);
        foreach (var (key, actual) in new[] { ("duration-a", 4m), ("duration-b", 7m) })
        {
            var reservation = await ReserveOneAsync(ledger, scope, key, actual, DurationDimension, Seconds);
            _ = await ledger.MarkStartedAsync(reservation);
            _ = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, actual));
        }

        var usage = (await ledger.GetSnapshotAsync(scope)).Usages.Single();
        usage.Committed.ToDecimalChecked().ShouldBe(11);
        usage.Unit.ShouldBe(Seconds);
    }

    /// <summary>Verifies descriptor unit validation applies even when the scope has no local limit for the dimension.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenUnlimitedDimensionUsesWrongUnit_RejectsBeforeMutation()
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-unit", null, UnlimitedDimension);
        var wrongUnit = new BudgetUnit("bytes");

        _ = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.ReserveBatchAsync(
            new BudgetLedgerBatchReserveRequest(scope, [Reservation(scope, "wrong-unit", 1, UnlimitedDimension, wrongUnit)])));

        (await ledger.GetSnapshotAsync(scope)).Usages.ShouldBeEmpty();
    }

    /// <summary>Verifies legal units for one dimension cannot be mixed within a batch or existing charged lineage.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenDimensionUnitsDiffer_RejectsWithoutConversion()
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-multi-unit", null, MultiUnitDimension);
        _ = await ReserveOneAsync(ledger, scope, "existing-count", 1, MultiUnitDimension, Count);
        _ = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.ReserveBatchAsync(
            new BudgetLedgerBatchReserveRequest(scope, [Reservation(scope, "later-bytes", 1, MultiUnitDimension, Bytes)])));
        (await ledger.GetSnapshotAsync(scope)).Usages.Single().Reserved.ToDecimalChecked().ShouldBe(1);
    }

    /// <summary>Verifies cumulative settlement beyond decimal range remains exact and blocks later work at a finite ceiling.</summary>
    [Fact]
    public async Task SettleAsync_WhenSumAggregateExceedsDecimal_PreservesExactAccounting()
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-settlement-overflow", decimal.MaxValue);
        var first = await ReserveOneAsync(ledger, scope, "overflow-first", 1, Dimension, Count);
        var second = await ReserveOneAsync(ledger, scope, "overflow-second", 1, Dimension, Count);
        _ = await ledger.MarkStartedAsync(first);
        _ = await ledger.MarkStartedAsync(second);
        _ = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(first, decimal.MaxValue));
        var secondCommit = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(second, 1));
        var exact = BudgetQuantity.FromDecimal(decimal.MaxValue).Add(BudgetQuantity.FromDecimal(1));
        var held = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(
            scope,
            [Reservation(scope, "overflow-later", 1)]))).ShouldBeOfType<BudgetLedgerBatchReserveHeld>();

        secondCommit.Actual.ShouldBe(1);
        (await ledger.GetSnapshotAsync(scope)).Usages.Single().Committed.ShouldBe(exact);
        held.Holds.ShouldHaveSingleItem().CurrentActual.ShouldBe(decimal.MaxValue);
    }

    /// <summary>Verifies exact aggregate magnitude alone does not block new work without a finite ceiling.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenExactAggregateExceedsDecimalWithoutLimit_RemainsAdmissible()
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-unlimited-exact", null);
        var first = await ReserveOneAsync(ledger, scope, "unlimited-exact-first", decimal.MaxValue, Dimension, Count);
        var second = await ReserveOneAsync(ledger, scope, "unlimited-exact-second", 1, Dimension, Count);
        _ = await ledger.MarkStartedAsync(first);
        _ = await ledger.MarkStartedAsync(second);
        _ = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(first, decimal.MaxValue));
        _ = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(second, 1));

        var later = await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(
            scope,
            [Reservation(scope, "unlimited-exact-later", 1)]));

        _ = later.ShouldBeOfType<BudgetLedgerBatchReserved>();
        (await ledger.GetSnapshotAsync(scope)).Usages.Single().Committed.ShouldBe(
            BudgetQuantity.FromDecimal(decimal.MaxValue).Add(BudgetQuantity.FromDecimal(1)));
    }

    /// <summary>Verifies corrections preserve exact aggregate usage beyond decimal range.</summary>
    [Fact]
    public async Task CorrectAsync_WhenSumAggregateExceedsDecimal_PreservesExactAccounting()
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-correction-overflow", decimal.MaxValue);
        var first = await ReserveOneAsync(ledger, scope, "correction-overflow-first", 1, Dimension, Count);
        var second = await ReserveOneAsync(ledger, scope, "correction-overflow-second", 1, Dimension, Count);
        _ = await ledger.MarkStartedAsync(first);
        _ = await ledger.MarkStartedAsync(second);
        _ = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(first, 0));
        _ = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(second, 0));
        _ = await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(first, decimal.MaxValue, 1));

        var correction = await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(second, 1, 1));

        correction.CorrectedActual.ShouldBe(1);
        (await ledger.GetSnapshotAsync(scope)).Usages.Single().Committed.ShouldBe(
            BudgetQuantity.FromDecimal(decimal.MaxValue).Add(BudgetQuantity.FromDecimal(1)));
    }

    /// <summary>Verifies measured reconciliation preserves exact aggregate usage beyond decimal range.</summary>
    [Fact]
    public async Task ReconcileAsync_WhenSumAggregateExceedsDecimal_PreservesExactAccounting()
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-reconcile-overflow", decimal.MaxValue);
        var first = await ReserveOneAsync(ledger, scope, "reconcile-overflow-first", 1, Dimension, Count);
        var second = await ReserveOneAsync(ledger, scope, "reconcile-overflow-second", 1, Dimension, Count);
        _ = await ledger.MarkStartedAsync(first);
        _ = await ledger.MarkStartedAsync(second);
        _ = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(first, decimal.MaxValue));
        var key = new IdempotencyKey("reconcile-overflow-key");

        var result = await ledger.ReconcileAsync(
            new BudgetLedgerReconciliationRequest(second, new BudgetActualMeasured(1), key));

        result.ShouldBeOfType<BudgetLedgerReconciliationSettled>().Commit.Actual.ShouldBe(1);
        (await ledger.GetSnapshotAsync(scope)).Usages.Single().Committed.ShouldBe(
            BudgetQuantity.FromDecimal(decimal.MaxValue).Add(BudgetQuantity.FromDecimal(1)));
    }

    /// <summary>Verifies original settlement and every correction revision retain independent exact replay identities.</summary>
    [Fact]
    public async Task SettleAsync_WhenCorrectedMultipleTimes_ReplaysOriginalAndOldCorrections()
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-corrections", 20);
        var reservation = await ReserveOneAsync(ledger, scope, "correction-item", 10, Dimension, Count);
        _ = await ledger.MarkStartedAsync(reservation);
        var original = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 8));
        var first = await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(reservation, 6, 1));
        _ = await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(reservation, 4, 2));

        (await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 8))).ShouldBe(original);
        (await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(reservation, 6, 1))).ShouldBe(first);
        _ = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.ReconcileAsync(
            new BudgetLedgerReconciliationRequest(reservation, new BudgetActualMeasured(4), new IdempotencyKey("fresh-terminal"))));
    }

    /// <summary>Verifies a recovery watermark excludes reservations started after the first page while allowing concurrent settlement omission.</summary>
    [Fact]
    public async Task ReadUnresolvedStartedAsync_WhenPaging_AnchorsWatermarkAndRejectsInvalidCursors()
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-paging", 20);
        var first = await ReserveOneAsync(ledger, scope, "page-a", 1, Dimension, Count);
        var second = await ReserveOneAsync(ledger, scope, "page-b", 1, Dimension, Count);
        _ = await ledger.MarkStartedAsync(first);
        _ = await ledger.MarkStartedAsync(second);
        var page = await ledger.ReadUnresolvedStartedAsync(new BudgetUnresolvedReservationQuery(scope, 1, null));
        var later = await ReserveOneAsync(ledger, scope, "page-later", 1, Dimension, Count);
        _ = await ledger.MarkStartedAsync(later);

        var remaining = await ledger.ReadUnresolvedStartedAsync(new BudgetUnresolvedReservationQuery(scope, 10, page.Next));
        remaining.Items.ShouldNotContain(item => item.Receipt.Reservation == later);
        var future = new BudgetReservationCursor(scope, new BudgetLedgerWatermark(page.Watermark.Value + 100), page.Items[0].Receipt.Reservation.Id);
        _ = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.ReadUnresolvedStartedAsync(new BudgetUnresolvedReservationQuery(scope, 1, future)));
        var unstarted = await ReserveOneAsync(ledger, scope, "page-unstarted", 1, Dimension, Count);
        var invalid = new BudgetReservationCursor(scope, page.Watermark, unstarted.Id);
        _ = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.ReadUnresolvedStartedAsync(new BudgetUnresolvedReservationQuery(scope, 1, invalid)));
        var laterAnchor = new BudgetReservationCursor(scope, page.Watermark, later.Id);
        _ = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.ReadUnresolvedStartedAsync(new BudgetUnresolvedReservationQuery(scope, 1, laterAnchor)));
        var foreignScope = new BudgetLedgerScopeReference(scope.Id, Address("foreign-tenant"));
        var foreign = new BudgetReservationCursor(foreignScope, page.Watermark, page.Items[0].Receipt.Reservation.Id);
        _ = await Should.ThrowAsync<BudgetLedgerReferenceUnavailableException>(async () => await ledger.ReadUnresolvedStartedAsync(new BudgetUnresolvedReservationQuery(foreignScope, 1, foreign)));
        var missing = new BudgetReservationCursor(scope, page.Watermark, new BudgetReservationId(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")));
        _ = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.ReadUnresolvedStartedAsync(new BudgetUnresolvedReservationQuery(scope, 1, missing)));
    }

    /// <summary>Verifies a parent recovery scan returns only directly owned rows even though child usage is charged to the parent.</summary>
    [Fact]
    public async Task ReadUnresolvedStartedAsync_WhenChildRowsExist_ReturnsOnlyExactScopeRows()
    {
        var ledger = new TFixture().CreateLedger();
        var parent = await CreateScopeAsync(ledger, "scan-parent", 10);
        var child = await CreateScopeAsync(ledger, "scan-child", null, parent: parent);
        var parentReservation = await ReserveOneAsync(ledger, parent, "scan-parent-item", 1, Dimension, Count);
        var childReservation = await ReserveOneAsync(ledger, child, "scan-child-item", 1, Dimension, Count);
        _ = await ledger.MarkStartedAsync(parentReservation);
        _ = await ledger.MarkStartedAsync(childReservation);

        var page = await ledger.ReadUnresolvedStartedAsync(new BudgetUnresolvedReservationQuery(parent, 10, null));

        page.Items.Select(item => item.Receipt.Reservation).ShouldBe([parentReservation]);
    }

    /// <summary>Verifies an honest expired start result is persisted and exactly replayed rather than becoming a terminal state exception.</summary>
    [Fact]
    public async Task MarkStartedAsync_WhenExpired_ReplaysTypedRejection()
    {
        var fixture = new TFixture();
        var ledger = fixture.CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-start-replay", 10);
        var request = Reservation(scope, "expired-start", 1) with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(1) };
        var reservation = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [request]))).ShouldBeOfType<BudgetLedgerBatchReserved>().Receipts[0].Reservation;
        fixture.Advance(TimeSpan.FromMinutes(1));

        var first = await ledger.MarkStartedAsync(reservation);
        var replay = await ledger.MarkStartedAsync(reservation);

        replay.ShouldBe(first);
        var expired = replay.ShouldBeOfType<BudgetStartExpired>();
        expired.ReservationId.ShouldBe(reservation.Id);
        expired.EffectiveExpiry.ShouldBe(request.ExpiresAt.Value);
    }

    /// <summary>Verifies snapshot cleanup preserves the ordinary typed expiry rejection for a later start attempt.</summary>
    [Fact]
    public async Task MarkStartedAsync_WhenSnapshotSweptExpiry_ReturnsTypedRejection()
    {
        var fixture = new TFixture();
        var ledger = fixture.CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-snapshot-expiry", 10);
        var request = Reservation(scope, "snapshot-expiry", 1) with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(1) };
        var reservation = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [request]))).ShouldBeOfType<BudgetLedgerBatchReserved>().Receipts[0].Reservation;
        fixture.Advance(TimeSpan.FromMinutes(1));
        _ = await ledger.GetSnapshotAsync(scope);

        var first = (await ledger.MarkStartedAsync(reservation)).ShouldBeOfType<BudgetStartExpired>();
        var replay = (await ledger.MarkStartedAsync(reservation)).ShouldBeOfType<BudgetStartExpired>();

        replay.ShouldBe(first);
    }

    /// <summary>Verifies caller-directed release remains distinct from ordinary expiry cleanup.</summary>
    [Fact]
    public async Task MarkStartedAsync_WhenExplicitlyReleased_RejectsStateTransition()
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-explicit-release", 10);
        var reservation = await ReserveOneAsync(ledger, scope, "explicit-release", 1, Dimension, Count);
        _ = (await ledger.ReleaseUnstartedAsync(reservation)).ShouldBeOfType<BudgetLedgerReleased>();

        _ = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.MarkStartedAsync(reservation));
    }

    /// <summary>Verifies settlement closes the start-permission replay window.</summary>
    [Fact]
    public async Task MarkStartedAsync_WhenAlreadySettled_RejectsStateTransition()
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-settled-start", 10);
        var reservation = await ReserveOneAsync(ledger, scope, "settled-start", 1, Dimension, Count);
        _ = await ledger.MarkStartedAsync(reservation);
        _ = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 1));

        _ = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.MarkStartedAsync(reservation));
    }

    /// <summary>Verifies concurrent child reservations cannot both consume their parent's final shared slot.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenChildrenRaceForLastParentSlot_AcceptsExactlyOne()
    {
        var ledger = new TFixture().CreateLedger();
        var parent = await CreateScopeAsync(ledger, "scope-parent", 1);
        var childA = await CreateScopeAsync(ledger, "scope-child-a", null, Dimension, Count, parent);
        var childB = await CreateScopeAsync(ledger, "scope-child-b", null, Dimension, Count, parent);
        var requests = new[]
        {
            new BudgetLedgerBatchReserveRequest(childA, [Reservation(childA, "child-a", 1)]),
            new BudgetLedgerBatchReserveRequest(childB, [Reservation(childB, "child-b", 1)]),
        };

        var results = await Task.WhenAll(requests.Select(async request => await ledger.ReserveBatchAsync(request)));

        results.Count(result => result is BudgetLedgerBatchReserved).ShouldBe(1);
        results.Count(result => result is BudgetLedgerBatchReserveRejected).ShouldBe(1);
    }

    /// <summary>Verifies an unused child snapshot reports only local configured limits while its parent still enforces inherited capacity.</summary>
    [Fact]
    public async Task GetSnapshotAsync_WhenChildHasOnlyInheritedLimit_ReportsNoLocalUsageUntilReserved()
    {
        var ledger = new TFixture().CreateLedger();
        var parent = await CreateScopeAsync(ledger, "snapshot-parent", 1);
        var child = await CreateScopeAsync(ledger, "snapshot-child", null, parent: parent);

        (await ledger.GetSnapshotAsync(child)).Usages.ShouldBeEmpty();
        _ = await ReserveOneAsync(ledger, child, "snapshot-child-first", 1, Dimension, Count);
        _ = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(child, [Reservation(child, "snapshot-child-second", 1)]))).ShouldBeOfType<BudgetLedgerBatchReserveRejected>();
    }

    /// <summary>Verifies independent tenant, principal, session, run, and operation addresses do not share capacity.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenAddressesDiffer_IsolatesEveryStructuralBoundary()
    {
        var ledger = new TFixture().CreateLedger();
        var operation = new OperationId(Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var addresses = new[]
        {
            Address("tenant-a"),
            new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal-b"), Agent, null, null, null),
            new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), Agent, new SessionId(Guid.Parse("30000000-0000-0000-0000-000000000001")), null, null),
            new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), Agent, null, new RunId(Guid.Parse("40000000-0000-0000-0000-000000000001")), null),
            new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), Agent, null, null, operation),
        };

        for (var index = 0; index < addresses.Length; index++)
        {
            var scope = await CreateScopeAsync(ledger, $"isolated-scope-{index}", 1, address: addresses[index]);
            _ = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [Reservation(scope, $"isolated-item-{index}", 1)]))).ShouldBeOfType<BudgetLedgerBatchReserved>();
        }
    }

    /// <summary>Verifies ordered batch replay is exact and altered evidence conflicts without another charge.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenReplayed_PreservesOrderedReceiptsAndRejectsAlteredEvidence()
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-replay", 10);
        var first = Reservation(scope, "a", 2);
        var second = Reservation(scope, "b", 3);
        var request = new BudgetLedgerBatchReserveRequest(scope, [first, second]);

        var original = await ledger.ReserveBatchAsync(request);
        var replay = await ledger.ReserveBatchAsync(request);
        var changed = new BudgetLedgerBatchReserveRequest(scope, [first, second with { Amount = 4 }]);
        var conflict = await Should.ThrowAsync<BudgetLedgerMutationConflictException>(async () => await ledger.ReserveBatchAsync(changed));
        var snapshot = await ledger.GetSnapshotAsync(scope);

        replay.ShouldBe(original);
        conflict.SafeMessage.ShouldNotBeNullOrWhiteSpace();
        snapshot.Usages.Single().Reserved.ToDecimalChecked().ShouldBe(5);
    }

    /// <summary>Verifies a rejected indivisible batch leaves every member unreserved.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenOneMemberExceedsLimit_RejectsWholeBatch()
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-atomic", 4);

        var result = await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope,
            [Reservation(scope, "a", 2), Reservation(scope, "b", 3)]));
        var snapshot = await ledger.GetSnapshotAsync(scope);

        _ = result.ShouldBeOfType<BudgetLedgerBatchReserveRejected>();
        snapshot.Usages.Single().Reserved.ToDecimalChecked().ShouldBe(0);
    }

    /// <summary>Verifies an ancestor open-row capacity failure binds no batch member or replay key and succeeds after release.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenAncestorOpenCapacityIsFull_RejectsAtomicallyWithoutReplayBinding()
    {
        var ledger = new TFixture().CreateLedger();
        var parent = await CreateScopeAsync(ledger, "scope-open-parent", null, maximumOpenReservations: 1);
        var child = await CreateScopeAsync(ledger, "scope-open-child", null, parent: parent);
        var existing = await ReserveOneAsync(ledger, child, "open-existing", 1, Dimension, Count);
        var laterRequest = new BudgetLedgerBatchReserveRequest(
            child,
            [Reservation(child, "open-later", 1, DurationDimension, Seconds)]);

        _ = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.ReserveBatchAsync(laterRequest));
        var afterFailure = await ledger.GetSnapshotAsync(child);
        afterFailure.Usages.ShouldNotContain(usage => usage.Dimension == DurationDimension);

        _ = (await ledger.ReleaseUnstartedAsync(existing)).ShouldBeOfType<BudgetLedgerReleased>();
        var retry = await ledger.ReserveBatchAsync(laterRequest);

        retry.ShouldBeOfType<BudgetLedgerBatchReserved>().Receipts.Length.ShouldBe(1);
    }

    /// <summary>Verifies started unknown usage stays charged until evidence settles it and exact evidence replays.</summary>
    [Fact]
    public async Task ReconcileAsync_WhenStartedUsageIsUnknown_RetainsThenSettlesOnce()
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-reconcile", 10);
        var reserved = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [Reservation(scope, "item", 5)]))).ShouldBeOfType<BudgetLedgerBatchReserved>();
        var reference = reserved.Receipts[0].Reservation;
        _ = await ledger.MarkStartedAsync(reference);
        var unknownRequest = new BudgetLedgerReconciliationRequest(reference, new BudgetStillUnknown(), new IdempotencyKey("unknown"));

        _ = (await ledger.ReleaseUnstartedAsync(reference)).ShouldBeOfType<BudgetLedgerRetainedStarted>();
        _ = (await ledger.ReconcileAsync(unknownRequest)).ShouldBeOfType<BudgetLedgerReconciliationRetainedUnknown>();
        var settledRequest = new BudgetLedgerReconciliationRequest(reference, new BudgetActualMeasured(3), new IdempotencyKey("measured"));
        var settled = await ledger.ReconcileAsync(settledRequest);
        var replay = await ledger.ReconcileAsync(settledRequest);

        replay.ShouldBe(settled);
        (await ledger.GetSnapshotAsync(scope)).Usages.Single().Committed.ToDecimalChecked().ShouldBe(3);
    }

    /// <summary>Verifies reconciliation keys are scoped per exact reservation and cannot disclose replay through a forged address.</summary>
    [Fact]
    public async Task ReconcileAsync_WhenKeyIsReusedAcrossReservations_IsIndependentAndAddressBound()
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-reconcile-axis", 10);
        var first = await ReserveOneAsync(ledger, scope, "axis-first", 1, Dimension, Count);
        var second = await ReserveOneAsync(ledger, scope, "axis-second", 1, Dimension, Count);
        _ = await ledger.MarkStartedAsync(first);
        _ = await ledger.MarkStartedAsync(second);
        var evidence = new BudgetStillUnknown();
        var key = new IdempotencyKey("same-reconciliation-key");

        _ = (await ledger.ReconcileAsync(new(first, evidence, key))).ShouldBeOfType<BudgetLedgerReconciliationRetainedUnknown>();
        _ = (await ledger.ReconcileAsync(new(second, evidence, key))).ShouldBeOfType<BudgetLedgerReconciliationRetainedUnknown>();
        _ = await Should.ThrowAsync<BudgetLedgerReferenceUnavailableException>(async () => await ledger.ReconcileAsync(new(
            new BudgetLedgerReservationReference(new(scope.Id, Address("foreign-tenant")), first.Id), evidence, key)));
        _ = await Should.ThrowAsync<BudgetLedgerMutationConflictException>(async () =>
            await ledger.ReconcileAsync(new(first, new BudgetNoUsageProven(), key)));
    }

    /// <summary>Verifies lazy expiry recomputes Maximum from surviving live values rather than subtracting maxima.</summary>
    [Theory]
    [InlineData(10, 8, true, false, 8)]
    [InlineData(10, 8, false, true, 10)]
    [InlineData(10, 10, true, false, 10)]
    [InlineData(10, 8, true, true, 0)]
    public async Task GetSnapshotAsync_WhenMaximumRowsExpire_RecomputesSurvivingMaximum(
        decimal firstAmount, decimal secondAmount, bool firstExpired, bool secondExpired, decimal expected)
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, $"scope-maximum-expiry-{firstAmount}-{secondAmount}-{firstExpired}-{secondExpired}",
            10, dimension: MaximumDimension);
        var operation = new OperationId(Guid.Parse("20000000-0000-0000-0000-000000000001"));
        BudgetReservationRequest Row(string key, decimal amount, bool expired) => new(scope.Id, MaximumDimension,
            amount, Count, operation, expired ? DateTimeOffset.UnixEpoch : DateTimeOffset.MaxValue, new(key));
        _ = (await ledger.ReserveBatchAsync(new(scope,
            [Row("maximum-expiry-first", firstAmount, firstExpired), Row("maximum-expiry-second", secondAmount, secondExpired)])))
            .ShouldBeOfType<BudgetLedgerBatchReserved>();

        var snapshot = await ledger.GetSnapshotAsync(scope);

        snapshot.Usages.Single().Reserved.ToDecimalChecked().ShouldBe(expected);
    }

    /// <summary>Verifies an address mismatch uses the same unavailable-reference category and discloses no snapshot.</summary>
    [Fact]
    public async Task GetSnapshotAsync_WhenIdentityExistsAtAnotherAddress_RejectsAsUnavailable()
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "scope-mask", 10);
        var foreign = new BudgetLedgerScopeReference(scope.Id, Address("another-tenant"));

        _ = await Should.ThrowAsync<BudgetLedgerReferenceUnavailableException>(async () => await ledger.GetSnapshotAsync(foreign));
        (await ledger.GetSnapshotAsync(scope)).ScopeId.ShouldBe(scope.Id);
    }

    /// <summary>Verifies one overrun creates independent holds using every charged boundary's captured policy.</summary>
    [Fact]
    public async Task SettleAsync_WhenAncestorPoliciesDiffer_CreatesPerBoundaryHolds()
    {
        var ledger = new TFixture().CreateLedger();
        var parent = await CreateScopeAsync(ledger, "hold-parent", 10, policy: BudgetOverrunHoldPolicy.RequireAuthorizedResolution);
        var child = await CreateScopeAsync(ledger, "hold-child", 10, parent: parent);
        var reservation = await ReserveOneAsync(ledger, child, "hold-row", 1, Dimension, Count);
        _ = await ledger.MarkStartedAsync(reservation);

        var commit = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 2));

        commit.AccountingRevision.HasValue.ShouldBeTrue();
        commit.CreatedOverrunHolds.Select(hold => hold.Policy).ShouldBe([
            BudgetOverrunHoldPolicy.ClearWhenReconciled,
            BudgetOverrunHoldPolicy.RequireAuthorizedResolution]);
        (await ledger.GetSnapshotAsync(child)).ActiveOverrunHolds.ShouldHaveSingleItem().Reference.Boundary.ShouldBe(child);
        (await ledger.GetSnapshotAsync(parent)).ActiveOverrunHolds.ShouldHaveSingleItem().Reference.Boundary.ShouldBe(parent);
        var held = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(child, [Reservation(child, "held-row", 1)])))
            .ShouldBeOfType<BudgetLedgerBatchReserveHeld>();
        held.Holds.Length.ShouldBe(2);
        _ = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(child,
            [Reservation(child, "unaffected-dimension", 1, MaximumDimension, Count)])))
            .ShouldBeOfType<BudgetLedgerBatchReserved>();
    }

    /// <summary>Verifies equality clears an automatic hold while fresh requested capacity remains independently enforced.</summary>
    [Fact]
    public async Task CorrectAsync_WhenAutomaticHoldBecomesEligible_ClearsExactGeneration()
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "auto-hold", 1);
        var reservation = await ReserveOneAsync(ledger, scope, "auto-row", 1, Dimension, Count);
        _ = await ledger.MarkStartedAsync(reservation);
        var commit = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 2));

        var correction = await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(reservation, 1, 1));

        correction.ClearedOverrunHolds.ShouldBe([commit.CreatedOverrunHolds.Single().Reference]);
        (await ledger.GetSnapshotAsync(scope)).ActiveOverrunHolds.ShouldBeEmpty();
        _ = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [Reservation(scope, "after-auto-clear", 1)])))
            .ShouldBeOfType<BudgetLedgerBatchReserveRejected>();
    }

    /// <summary>
    /// Verifies an expired-but-unswept unstarted reservation is excluded from the hard-limit accounting
    /// a correction uses to decide whether a ClearWhenReconciled hold becomes eligible to clear.
    /// </summary>
    [Fact]
    public async Task CorrectAsync_WhenAnotherReservationExpiredButUnswept_StillClearsAnEligibleHold()
    {
        var fixture = new TFixture();
        var ledger = fixture.CreateLedger();
        var scope = await CreateScopeAsync(ledger, "expired-clear-eligibility", 100);
        var expiring = Reservation(scope, "expired-row", 60) with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(1) };
        _ = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [expiring])))
            .ShouldBeOfType<BudgetLedgerBatchReserved>();
        fixture.Advance(TimeSpan.FromMinutes(10));
        // The expired row above is never swept by a reserve/snapshot/mark-started call before the
        // correction below, so it stays "IsCapacityRetaining" unless the correction path itself
        // excludes it.
        var reservation = await ReserveOneAsync(ledger, scope, "settled-row", 45, Dimension, Count);
        _ = await ledger.MarkStartedAsync(reservation);
        var commit = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 50));
        _ = commit.CreatedOverrunHolds.ShouldHaveSingleItem();

        var correction = await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(reservation, 45, 1));

        correction.ClearedOverrunHolds.ShouldBe([commit.CreatedOverrunHolds.Single().Reference]);
        (await ledger.GetSnapshotAsync(scope)).ActiveOverrunHolds.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies an expired-but-unswept unstarted reservation is excluded from the hard-limit accounting
    /// an operator resolution uses to decide whether the boundary still has a hard-limit failure.
    /// </summary>
    [Fact]
    public async Task ResolveOverrunHoldAsync_WhenAnotherReservationExpiredButUnswept_StillResolvesAnEligibleHold()
    {
        var fixture = new TFixture();
        var ledger = fixture.CreateLedger();
        var scope = await CreateScopeAsync(ledger, "expired-resolve-eligibility", 100, policy: BudgetOverrunHoldPolicy.RequireAuthorizedResolution);
        var expiring = Reservation(scope, "expired-row", 60) with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(1) };
        _ = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [expiring])))
            .ShouldBeOfType<BudgetLedgerBatchReserved>();
        fixture.Advance(TimeSpan.FromMinutes(10));
        var reservation = await ReserveOneAsync(ledger, scope, "settled-row", 45, Dimension, Count);
        _ = await ledger.MarkStartedAsync(reservation);
        var commit = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 50));
        var hold = commit.CreatedOverrunHolds.ShouldHaveSingleItem().Reference;
        _ = await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(reservation, 45, 1));

        var resolution = await ledger.ResolveOverrunHoldAsync(ResolutionRequest(hold, "expired-resolve", "operator", 1));

        _ = resolution.ShouldBeOfType<BudgetOverrunHoldResolved>();
    }

    /// <summary>Verifies operator resolution waits for eligible truth, replays exactly, and never clears a later generation.</summary>
    [Fact]
    public async Task ResolveOverrunHoldAsync_WhenAccountingBecomesEligible_PreservesGenerationReplay()
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "operator-hold", 10, policy: BudgetOverrunHoldPolicy.RequireAuthorizedResolution);
        var reservation = await ReserveOneAsync(ledger, scope, "operator-row", 1, Dimension, Count);
        _ = await ledger.MarkStartedAsync(reservation);
        var commit = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 2));
        var oldHold = commit.CreatedOverrunHolds.Single().Reference;
        _ = (await ledger.ResolveOverrunHoldAsync(ResolutionRequest(oldHold, "blocked-resolution", "operator-a", 4)))
            .ShouldBeOfType<BudgetOverrunHoldResolutionBlocked>();
        _ = await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(reservation, 1, 1));
        var request = ResolutionRequest(oldHold, "successful-resolution", "operator-b", 5);

        var resolved = (await ledger.ResolveOverrunHoldAsync(request)).ShouldBeOfType<BudgetOverrunHoldResolved>();
        (await ledger.ResolveOverrunHoldAsync(request)).ShouldBe(resolved);
        _ = await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(reservation, 2, 2));
        var current = (await ledger.GetSnapshotAsync(scope)).ActiveOverrunHolds.ShouldHaveSingleItem();

        current.Reference.TriggeringRevision.ShouldNotBe(oldHold.TriggeringRevision);
        (await ledger.ResolveOverrunHoldAsync(request)).ShouldBe(resolved);
        _ = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.ResolveOverrunHoldAsync(
            ResolutionRequest(oldHold, "historical-resolution", "operator-c", 6)));
        (await ledger.GetSnapshotAsync(scope)).ActiveOverrunHolds.ShouldHaveSingleItem().Reference.ShouldBe(current.Reference);
    }

    /// <summary>Verifies automatic and operator clearance both wait until every row overrun at the boundary is corrected.</summary>
    [Theory]
    [InlineData(BudgetOverrunHoldPolicy.ClearWhenReconciled)]
    [InlineData(BudgetOverrunHoldPolicy.RequireAuthorizedResolution)]
    public async Task CorrectAsync_WhenAnotherRowStillOverruns_PreservesAllBoundaryHolds(BudgetOverrunHoldPolicy policy)
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, $"several-holds-{policy}", 10, policy: policy);
        var first = await ReserveOneAsync(ledger, scope, $"several-first-{policy}", 1, Dimension, Count);
        var second = await ReserveOneAsync(ledger, scope, $"several-second-{policy}", 1, Dimension, Count);
        _ = await ledger.MarkStartedAsync(first);
        _ = await ledger.MarkStartedAsync(second);
        var firstCommit = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(first, 2));
        _ = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(second, 2));

        var firstCorrection = await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(first, 1, 1));

        firstCorrection.ClearedOverrunHolds.ShouldBeEmpty();
        (await ledger.GetSnapshotAsync(scope)).ActiveOverrunHolds.Length.ShouldBe(2);
        if (policy == BudgetOverrunHoldPolicy.RequireAuthorizedResolution)
        {
            _ = (await ledger.ResolveOverrunHoldAsync(ResolutionRequest(firstCommit.CreatedOverrunHolds.Single().Reference, $"several-blocked-{policy}", "operator", 7)))
                .ShouldBeOfType<BudgetOverrunHoldResolutionBlocked>();
        }

        var secondCorrection = await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(second, 1, 1));
        if (policy == BudgetOverrunHoldPolicy.ClearWhenReconciled)
        {
            secondCorrection.ClearedOverrunHolds.Length.ShouldBe(2);
            (await ledger.GetSnapshotAsync(scope)).ActiveOverrunHolds.ShouldBeEmpty();
        }
        else
        {
            secondCorrection.ClearedOverrunHolds.ShouldBeEmpty();
            (await ledger.GetSnapshotAsync(scope)).ActiveOverrunHolds.Length.ShouldBe(2);
        }
    }

    /// <summary>Verifies cancellation and structurally mismatched audit evidence cannot resolve an eligible hold.</summary>
    [Fact]
    public async Task ResolveOverrunHoldAsync_WhenAdmissionFailsBeforeMutation_PreservesEligibleHold()
    {
        var ledger = new TFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "resolution-atomic", 10, policy: BudgetOverrunHoldPolicy.RequireAuthorizedResolution);
        var reservation = await ReserveOneAsync(ledger, scope, "resolution-atomic-row", 1, Dimension, Count);
        _ = await ledger.MarkStartedAsync(reservation);
        var commit = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 2));
        var hold = commit.CreatedOverrunHolds.Single().Reference;
        _ = await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(reservation, 1, 1));
        var valid = ResolutionRequest(hold, "resolution-atomic-valid", "operator", 8);
        var otherRevision = new BudgetOverrunHoldReference(hold.Boundary, hold.Reservation, new BudgetAccountingRevision(checked(hold.TriggeringRevision.Value + 1)));
        var mismatchedReceipt = ResolutionRequest(otherRevision, "resolution-atomic-wrong", "operator", 9).EnforcementReceipt;
        var mismatched = new BudgetOverrunHoldResolutionRequest(hold, mismatchedReceipt, new IdempotencyKey("resolution-atomic-wrong"));

        _ = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.ResolveOverrunHoldAsync(mismatched));
        using var source = new CancellationTokenSource();
        source.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await ledger.ResolveOverrunHoldAsync(valid, source.Token));
        (await ledger.GetSnapshotAsync(scope)).ActiveOverrunHolds.ShouldHaveSingleItem().Reference.ShouldBe(hold);

        _ = (await ledger.ResolveOverrunHoldAsync(valid)).ShouldBeOfType<BudgetOverrunHoldResolved>();
    }

    private static async Task<BudgetLedgerScopeReference> CreateScopeAsync(
        IBudgetLedger ledger,
        string key,
        decimal? limit,
        BudgetDimension? dimension = null,
        BudgetUnit? unit = null,
        BudgetLedgerScopeReference? parent = null,
        BudgetScopeAddress? address = null,
        int maximumOpenReservations = 32,
        BudgetOverrunHoldPolicy policy = BudgetOverrunHoldPolicy.ClearWhenReconciled)
    {
        var limits = limit is { } value
            ? ImmutableArray.Create(new BudgetLimit(dimension ?? Dimension, value, unit ?? Count, BudgetLimitKind.Hard))
            : [];
        var request = new BudgetScopeRequest(parent?.Id, address ?? parent?.Address ?? Address("tenant"), limits, new IdempotencyKey(key));
        var result = await ledger.CreateScopeAsync(new BudgetLedgerScopeCreateRequest(request, new BudgetScopeAdmission(8, maximumOpenReservations, TimeSpan.FromMinutes(5), policy)));
        return result.ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
    }

    private static BudgetScopeAddress Address(string tenant) => new(new TenantId(tenant), new PrincipalId("principal"), Agent, null, null, null);

    private static BudgetReservationRequest Reservation(BudgetLedgerScopeReference scope, string key, decimal amount) =>
        Reservation(scope, key, amount, Dimension, Count);

    private static BudgetReservationRequest Reservation(BudgetLedgerScopeReference scope, string key, decimal amount, BudgetDimension dimension, BudgetUnit unit) =>
        new(scope.Id, dimension, amount, unit, new OperationId(Guid.Parse("20000000-0000-0000-0000-000000000001")), null, new IdempotencyKey(key));

    private static async Task<BudgetLedgerReservationReference> ReserveOneAsync(
        IBudgetLedger ledger,
        BudgetLedgerScopeReference scope,
        string key,
        decimal amount,
        BudgetDimension dimension,
        BudgetUnit unit) =>
        (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [Reservation(scope, key, amount, dimension, unit)])))
            .ShouldBeOfType<BudgetLedgerBatchReserved>().Receipts[0].Reservation;

    private static BudgetOverrunHoldResolutionRequest ResolutionRequest(BudgetOverrunHoldReference hold, string key, string principal, int intentSuffix)
    {
        var securityScope = new SecurityAuthorizationScope(
            Agent,
            null,
            new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("30000000-0000-0000-0000-000000000003")), null));
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId(principal), ExecutionSubjectKind.Human);
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
            new SecurityEnforcementIntentId(Guid.Parse($"40000000-0000-0000-0000-{intentSuffix:000000000000}")),
            new GrantId(Guid.Parse("50000000-0000-0000-0000-000000000005")),
            new SecurityRequestId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
            enforcement,
            null,
            new ContentHash($"sha256:{key}"),
            DateTimeOffset.UnixEpoch);
        return new BudgetOverrunHoldResolutionRequest(hold, receipt, new IdempotencyKey(key));
    }
}
