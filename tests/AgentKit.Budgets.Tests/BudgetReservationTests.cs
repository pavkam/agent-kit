// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Tests;



/// <summary>Verifies BudgetReservation behavior and contracts.</summary>
public sealed class BudgetReservationTests
{
    [Fact]
    public async Task CommitAndCorrect_WhenArgumentsAreInvalid_RejectBeforeLedger()
    {
        var ledger = new RecordingLedger();
        var scope = Scope();
        var reservation = new BudgetReservation(ledger, Receipt(scope, TestFactory.ReservationRequest(scope.Id)));
        (await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await reservation.CommitAsync(-1m))).ParamName.ShouldBe("actual");
        (await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await reservation.CorrectAsync(-1m, 1))).ParamName.ShouldBe("correctedActual");
        (await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await reservation.CorrectAsync(0m, 0))).ParamName.ShouldBe("revision");
        ledger.Calls.ShouldBe(0);
    }

    private static BudgetLedgerScopeReference Scope() => new(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address());
    private static BudgetLedgerReservationReceipt Receipt(BudgetLedgerScopeReference scope, BudgetReservationRequest request) => new(new BudgetLedgerReservationReference(scope, new BudgetReservationId(Guid.NewGuid())), request, new BudgetEffectiveReservation(DateTimeOffset.UtcNow.AddMinutes(1)));
    private sealed class RecordingLedger: IBudgetLedger
    {
        public BudgetLedgerDescriptor? DescriptorValue { get; set; } = new(false, BudgetLedgerConcurrencyDomain.ProcessLocal);
        public int DescriptorReads { get; private set; }

        public BudgetLedgerDescriptor Descriptor
        {
            get
            {
                DescriptorReads++;
                return DescriptorValue!;
            }
        }

        public int Calls { get; private set; }
        public bool Cancel { get; init; }
        public BudgetLedgerScopeCreateResult CreateResult { get; set; } = null!;
        public BudgetLedgerBatchReserveResult ReserveResult { get; set; } = null!;
        public BudgetStartResult StartResult { get; set; } = null!;
        public BudgetCommitResult CommitResult { get; set; } = null!;
        public BudgetCorrectionResult CorrectionResult { get; set; } = null!;
        public BudgetSnapshot SnapshotResult { get; set; } = null!;
        public BudgetLedgerReleaseResult ReleaseResult { get; set; } = null!;
        public CancellationToken CreateToken { get; private set; }
        public CancellationToken ReserveToken { get; private set; }
        public CancellationToken StartToken { get; private set; }
        public CancellationToken SettleToken { get; private set; }
        public CancellationToken CorrectToken { get; private set; }
        public CancellationToken SnapshotToken { get; private set; }
        public CancellationToken ReleaseToken { get; private set; }

        public ValueTask<BudgetLedgerScopeCreateResult> CreateScopeAsync(BudgetLedgerScopeCreateRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            CreateToken = cancellationToken;
            return Result(CreateResult, cancellationToken);
        }

        public ValueTask<BudgetLedgerBatchReserveResult> ReserveBatchAsync(BudgetLedgerBatchReserveRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            ReserveToken = cancellationToken;
            return Result(ReserveResult, cancellationToken);
        }

        public ValueTask<BudgetStartResult> MarkStartedAsync(BudgetLedgerReservationReference reservation, CancellationToken cancellationToken = default)
        {
            Calls++;
            StartToken = cancellationToken;
            return Result(StartResult, cancellationToken);
        }

        public ValueTask<BudgetCommitResult> SettleAsync(BudgetLedgerSettlementRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            SettleToken = cancellationToken;
            return Result(CommitResult, cancellationToken);
        }

        public ValueTask<BudgetCorrectionResult> CorrectAsync(BudgetLedgerCorrectionRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            CorrectToken = cancellationToken;
            return Result(CorrectionResult, cancellationToken);
        }

        public ValueTask<BudgetSnapshot> GetSnapshotAsync(BudgetLedgerScopeReference scope, CancellationToken cancellationToken = default)
        {
            Calls++;
            SnapshotToken = cancellationToken;
            return Result(SnapshotResult, cancellationToken);
        }

        public ValueTask<BudgetLedgerReleaseResult> ReleaseUnstartedAsync(BudgetLedgerReservationReference reservation, CancellationToken cancellationToken = default)
        {
            Calls++;
            ReleaseToken = cancellationToken;
            return Result(ReleaseResult, cancellationToken);
        }

        public ValueTask<BudgetUnresolvedReservationPage> ReadUnresolvedStartedAsync(BudgetUnresolvedReservationQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetLedgerReconciliationResult> ReconcileAsync(BudgetLedgerReconciliationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetOverrunHoldResolutionResult> ResolveOverrunHoldAsync(BudgetOverrunHoldResolutionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        private ValueTask<T> Result<T>(T value, CancellationToken token) => Cancel ? ValueTask.FromCanceled<T>(token) : ValueTask.FromResult(value);
    }

    [Fact]
    public async Task Reservation_WhenUsed_DelegatesExactReceiptAndDisposalRetainsLedgerSemantics()
    {
        var ledger = new RecordingBudgetLedger();
        var scope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address());
        var request = TestFactory.ReservationRequest(scope.Id, 3m);
        var receipt = TestFactory.Receipt(scope, request);
        var reservation = new BudgetReservation(ledger, receipt);
        ledger.StartResult = new BudgetStarted(receipt.Reservation.Id, false);
        ledger.CommitResult = new BudgetCommitResult(receipt.Reservation.Id, 3m, 2m, 1m, 0m);
        ledger.CorrectionResult = new BudgetCorrectionResult(receipt.Reservation.Id, 2m, 1m, 1);
        ledger.ReleaseResult = new BudgetLedgerRetainedStarted(receipt.Reservation);
        _ = await reservation.MarkStartedAsync(TestContext.Current.CancellationToken);
        var commit = await reservation.CommitAsync(2m, TestContext.Current.CancellationToken);
        var correction = await reservation.CorrectAsync(1m, 1, TestContext.Current.CancellationToken);
        await reservation.DisposeAsync();
        reservation.Id.ShouldBe(receipt.Reservation.Id);
        reservation.ScopeId.ShouldBe(scope.Id);
        reservation.Dimension.ShouldBe(request.Dimension);
        reservation.Reserved.ShouldBe(3m);
        ledger.StartReference.ShouldBe(receipt.Reservation);
        ledger.SettlementRequest!.Actual.ShouldBe(2m);
        ledger.CorrectionRequest!.CorrectedActual.ShouldBe(1m);
        ledger.ReleaseReference.ShouldBe(receipt.Reservation);
        commit.ShouldBeSameAs(ledger.CommitResult);
        correction.ShouldBeSameAs(ledger.CorrectionResult);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task DisposeAsync_WhenLedgerPersistenceFails_RetryForwardsExactReferenceWithoutCancellation(bool acknowledgementUnknown, bool retainStarted)
    {
        var failure = new BudgetLedgerPersistenceUnavailableException("unavailable", acknowledgementUnknown);
        var ledger = new RecordingBudgetLedger
        {
            ReleaseException = failure
        };
        var scope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address());
        var receipt = TestFactory.Receipt(scope, TestFactory.ReservationRequest(scope.Id));
        var reservation = new BudgetReservation(ledger, receipt);
        var exception = await Should.ThrowAsync<BudgetLedgerPersistenceUnavailableException>(DisposeAsync);
        ledger.ReleaseException = null;
        ledger.ReleaseResult = retainStarted ? new BudgetLedgerRetainedStarted(ledger.ReleaseReferences.Single()) : new BudgetLedgerReleased(ledger.ReleaseReferences.Single());
        await reservation.DisposeAsync();
        exception.ShouldBeSameAs(failure);
        ledger.ReleaseReferences.Count.ShouldBe(2);
        ledger.ReleaseReferences.ShouldAllBe(reference => reference == receipt.Reservation);
        ledger.ReleaseTokens.ShouldAllBe(static token => token == CancellationToken.None);
        async Task DisposeAsync() => await reservation.DisposeAsync();
    }



    [Fact]
    public void Constructors_WhenRequiredDependencyIsNull_ThrowExactParameterName()
    {
        var ledger = new RecordingLedger();
        var scope = Scope();
        var request = TestFactory.ReservationRequest(scope.Id);
        Should.Throw<ArgumentNullException>(() => new BudgetReservation(null!, Receipt(scope, request))).ParamName.ShouldBe("ledger");
        Should.Throw<ArgumentNullException>(() => new BudgetReservation(ledger, null!)).ParamName.ShouldBe("receipt");
    }
}
