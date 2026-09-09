// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Tests;

public sealed class BudgetRuntimeBoundaryTests
{
    [Fact]
    public void Constructors_WhenRequiredDependencyIsNull_ThrowExactParameterName()
    {
        var ledger = new RecordingLedger();
        var scope = Scope();
        var request = TestFactory.ReservationRequest(scope.Id);

        Should.Throw<ArgumentNullException>(() => new BudgetAuthority(null!, TestFactory.DefaultOptions())).ParamName.ShouldBe("ledger");
        Should.Throw<ArgumentNullException>(() => new BudgetAuthority(ledger, null!)).ParamName.ShouldBe("options");
        Should.Throw<ArgumentNullException>(() => new BudgetScope(null!, scope)).ParamName.ShouldBe("ledger");
        Should.Throw<ArgumentNullException>(() => new BudgetScope(ledger, null!)).ParamName.ShouldBe("reference");
        Should.Throw<ArgumentNullException>(() => new BudgetReservation(null!, Receipt(scope, request))).ParamName.ShouldBe("ledger");
        Should.Throw<ArgumentNullException>(() => new BudgetReservation(ledger, null!)).ParamName.ShouldBe("receipt");
    }

    [Fact]
    public async Task CreateAndReserve_WhenRequestIsNullOrBatchInvalid_RejectBeforeLedger()
    {
        var ledger = new RecordingLedger();
        var authority = new BudgetAuthority(ledger, TestFactory.DefaultOptions());
        var scope = new BudgetScope(ledger, Scope());

        (await Should.ThrowAsync<ArgumentNullException>(async () => await authority.CreateChildScopeAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await scope.ReserveAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentException>(async () => await scope.ReserveBatchAsync([]))).ParamName.ShouldBe("originalRequests");
        (await Should.ThrowAsync<ArgumentException>(async () => await scope.ReserveBatchAsync(default))).ParamName.ShouldBe("originalRequests");
        var copiedInvalid = TestFactory.ReservationRequest(scope.Id) with { Amount = -1m };
        (await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await scope.ReserveAsync(copiedInvalid))).ParamName.ShouldBe("originalRequests");
        ledger.Calls.ShouldBe(0);
    }

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

    [Fact]
    public async Task RuntimeOperations_WhenSuccessful_ForwardCallerTokensAndDisposeUsesNone()
    {
        var ledger = new RecordingLedger();
        var reference = Scope();
        var request = TestFactory.ReservationRequest(reference.Id);
        var receipt = Receipt(reference, request);
        ledger.CreateResult = new BudgetLedgerScopeCreated(reference);
        ledger.ReserveResult = new BudgetLedgerBatchReserved([receipt]);
        ledger.StartResult = new BudgetStarted(receipt.Reservation.Id, false);
        ledger.CommitResult = new BudgetCommitResult(receipt.Reservation.Id, 1m, 1m, 0m, 0m);
        ledger.CorrectionResult = new BudgetCorrectionResult(receipt.Reservation.Id, 1m, 0m, 1);
        ledger.SnapshotResult = new BudgetSnapshot(reference.Id, DateTimeOffset.UnixEpoch, []);
        ledger.ReleaseResult = new BudgetLedgerReleased(receipt.Reservation);
        var authority = new BudgetAuthority(ledger, TestFactory.DefaultOptions());
        using var create = new CancellationTokenSource(); using var reserve = new CancellationTokenSource();
        using var start = new CancellationTokenSource(); using var settle = new CancellationTokenSource();
        using var correct = new CancellationTokenSource(); using var snapshot = new CancellationTokenSource();

        var scope = ((BudgetScopeCreated) await authority.CreateChildScopeAsync(TestFactory.ScopeRequest(), create.Token)).Scope;
        var reservation = ((BudgetReserved) await scope.ReserveAsync(request, reserve.Token)).Reservation;
        _ = await reservation.MarkStartedAsync(start.Token);
        _ = await reservation.CommitAsync(1m, settle.Token);
        _ = await reservation.CorrectAsync(1m, 1, correct.Token);
        _ = await scope.GetSnapshotAsync(snapshot.Token);
        await reservation.DisposeAsync();

        ledger.CreateToken.ShouldBe(create.Token); ledger.ReserveToken.ShouldBe(reserve.Token);
        ledger.StartToken.ShouldBe(start.Token); ledger.SettleToken.ShouldBe(settle.Token);
        ledger.CorrectToken.ShouldBe(correct.Token); ledger.SnapshotToken.ShouldBe(snapshot.Token);
        ledger.ReleaseToken.ShouldBe(CancellationToken.None);
    }

    [Fact]
    public async Task RuntimeOperations_WhenLedgerAsynchronouslyCancels_PropagateCancellation()
    {
        using var source = new CancellationTokenSource(); source.Cancel();
        var ledger = new RecordingLedger { Cancel = true };
        var reference = Scope();
        var receipt = Receipt(reference, TestFactory.ReservationRequest(reference.Id));
        var authority = new BudgetAuthority(ledger, TestFactory.DefaultOptions());
        var scope = new BudgetScope(ledger, reference);
        var reservation = new BudgetReservation(ledger, receipt);

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await authority.CreateChildScopeAsync(TestFactory.ScopeRequest(), source.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await scope.ReserveAsync(receipt.OriginalRequest, source.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await reservation.MarkStartedAsync(source.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await reservation.CommitAsync(0m, source.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await reservation.CorrectAsync(0m, 1, source.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await scope.GetSnapshotAsync(source.Token));
    }

    private static BudgetLedgerScopeReference Scope() => new(new BudgetScopeId(Guid.NewGuid()), TestFactory.Address());
    private static BudgetLedgerReservationReceipt Receipt(BudgetLedgerScopeReference scope, BudgetReservationRequest request) =>
        new(new BudgetLedgerReservationReference(scope, new BudgetReservationId(Guid.NewGuid())), request, new BudgetEffectiveReservation(DateTimeOffset.UtcNow.AddMinutes(1)));

    private sealed class RecordingLedger: IBudgetLedger
    {
        public int Calls { get; private set; }
        public bool Cancel { get; init; }
        public BudgetLedgerScopeCreateResult CreateResult { get; set; } = null!; public BudgetLedgerBatchReserveResult ReserveResult { get; set; } = null!;
        public BudgetStartResult StartResult { get; set; } = null!; public BudgetCommitResult CommitResult { get; set; } = null!; public BudgetCorrectionResult CorrectionResult { get; set; } = null!; public BudgetSnapshot SnapshotResult { get; set; } = null!; public BudgetLedgerReleaseResult ReleaseResult { get; set; } = null!;
        public CancellationToken CreateToken { get; private set; }
        public CancellationToken ReserveToken { get; private set; }
        public CancellationToken StartToken { get; private set; }
        public CancellationToken SettleToken { get; private set; }
        public CancellationToken CorrectToken { get; private set; }
        public CancellationToken SnapshotToken { get; private set; }
        public CancellationToken ReleaseToken { get; private set; }
        public ValueTask<BudgetLedgerScopeCreateResult> CreateScopeAsync(BudgetLedgerScopeCreateRequest request, CancellationToken cancellationToken = default) { Calls++; CreateToken = cancellationToken; return Result(CreateResult, cancellationToken); }
        public ValueTask<BudgetLedgerBatchReserveResult> ReserveBatchAsync(BudgetLedgerBatchReserveRequest request, CancellationToken cancellationToken = default) { Calls++; ReserveToken = cancellationToken; return Result(ReserveResult, cancellationToken); }
        public ValueTask<BudgetStartResult> MarkStartedAsync(BudgetLedgerReservationReference reservation, CancellationToken cancellationToken = default) { Calls++; StartToken = cancellationToken; return Result(StartResult, cancellationToken); }
        public ValueTask<BudgetCommitResult> SettleAsync(BudgetLedgerSettlementRequest request, CancellationToken cancellationToken = default) { Calls++; SettleToken = cancellationToken; return Result(CommitResult, cancellationToken); }
        public ValueTask<BudgetCorrectionResult> CorrectAsync(BudgetLedgerCorrectionRequest request, CancellationToken cancellationToken = default) { Calls++; CorrectToken = cancellationToken; return Result(CorrectionResult, cancellationToken); }
        public ValueTask<BudgetSnapshot> GetSnapshotAsync(BudgetLedgerScopeReference scope, CancellationToken cancellationToken = default) { Calls++; SnapshotToken = cancellationToken; return Result(SnapshotResult, cancellationToken); }
        public ValueTask<BudgetLedgerReleaseResult> ReleaseUnstartedAsync(BudgetLedgerReservationReference reservation, CancellationToken cancellationToken = default) { Calls++; ReleaseToken = cancellationToken; return Result(ReleaseResult, cancellationToken); }
        public ValueTask<BudgetUnresolvedReservationPage> ReadUnresolvedStartedAsync(BudgetUnresolvedReservationQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetLedgerReconciliationResult> ReconcileAsync(BudgetLedgerReconciliationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetOverrunHoldResolutionResult> ResolveOverrunHoldAsync(BudgetOverrunHoldResolutionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        private ValueTask<T> Result<T>(T value, CancellationToken token) => Cancel ? ValueTask.FromCanceled<T>(token) : ValueTask.FromResult(value);
    }
}
