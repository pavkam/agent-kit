// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Tests;

internal sealed class RecordingBudgetLedger: IBudgetLedger
{
    public BudgetLedgerDescriptor Descriptor { get; } = new(false, BudgetLedgerConcurrencyDomain.ProcessLocal);
    public BudgetLedgerScopeCreateResult CreateResult { get; set; } = null!;
    public BudgetLedgerBatchReserveResult ReserveResult { get; set; } = null!;
    public BudgetStartResult StartResult { get; set; } = null!;
    public BudgetCommitResult CommitResult { get; set; } = null!;
    public BudgetCorrectionResult CorrectionResult { get; set; } = null!;
    public BudgetLedgerReleaseResult ReleaseResult { get; set; } = null!;
    public BudgetSnapshot SnapshotResult { get; set; } = null!;
    public Exception? ReleaseException { get; set; }
    public Exception? CreateException { get; set; }
    public Exception? StartException { get; set; }
    public Exception? SettleException { get; set; }
    public Exception? CorrectException { get; set; }
    public Exception? ReserveException { get; set; }
    public Exception? SnapshotException { get; set; }
    public BudgetLedgerScopeCreateRequest? CreateRequest { get; private set; }
    public BudgetLedgerBatchReserveRequest? ReserveRequest { get; private set; }
    public BudgetLedgerReservationReference? StartReference { get; private set; }
    public BudgetLedgerSettlementRequest? SettlementRequest { get; private set; }
    public BudgetLedgerCorrectionRequest? CorrectionRequest { get; private set; }
    public BudgetLedgerReservationReference? ReleaseReference { get; private set; }
    public List<BudgetLedgerReservationReference> ReleaseReferences { get; } = [];
    public List<CancellationToken> ReleaseTokens { get; } = [];

    public ValueTask<BudgetLedgerScopeCreateResult> CreateScopeAsync(BudgetLedgerScopeCreateRequest request, CancellationToken cancellationToken = default)
    {
        CreateRequest = request;
        return CreateException is null ? ValueTask.FromResult(CreateResult) : ValueTask.FromException<BudgetLedgerScopeCreateResult>(CreateException);
    }

    public ValueTask<BudgetLedgerBatchReserveResult> ReserveBatchAsync(BudgetLedgerBatchReserveRequest request, CancellationToken cancellationToken = default)
    {
        ReserveRequest = request;
        return ReserveException is null ? ValueTask.FromResult(ReserveResult) : ValueTask.FromException<BudgetLedgerBatchReserveResult>(ReserveException);
    }

    public ValueTask<BudgetStartResult> MarkStartedAsync(BudgetLedgerReservationReference reservation, CancellationToken cancellationToken = default)
    {
        StartReference = reservation;
        return StartException is null ? ValueTask.FromResult(StartResult) : ValueTask.FromException<BudgetStartResult>(StartException);
    }

    public ValueTask<BudgetCommitResult> SettleAsync(BudgetLedgerSettlementRequest request, CancellationToken cancellationToken = default)
    {
        SettlementRequest = request;
        return SettleException is null ? ValueTask.FromResult(CommitResult) : ValueTask.FromException<BudgetCommitResult>(SettleException);
    }

    public ValueTask<BudgetLedgerReleaseResult> ReleaseUnstartedAsync(BudgetLedgerReservationReference reservation, CancellationToken cancellationToken = default)
    {
        ReleaseReference = reservation;
        ReleaseReferences.Add(reservation);
        ReleaseTokens.Add(cancellationToken);
        return ReleaseException is null ? ValueTask.FromResult(ReleaseResult) : ValueTask.FromException<BudgetLedgerReleaseResult>(ReleaseException);
    }

    public ValueTask<BudgetCorrectionResult> CorrectAsync(BudgetLedgerCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        CorrectionRequest = request;
        return CorrectException is null ? ValueTask.FromResult(CorrectionResult) : ValueTask.FromException<BudgetCorrectionResult>(CorrectException);
    }

    public ValueTask<BudgetSnapshot> GetSnapshotAsync(BudgetLedgerScopeReference scope, CancellationToken cancellationToken = default) =>
        SnapshotException is null ? ValueTask.FromResult(SnapshotResult) : ValueTask.FromException<BudgetSnapshot>(SnapshotException);
    public ValueTask<BudgetUnresolvedReservationPage> ReadUnresolvedStartedAsync(BudgetUnresolvedReservationQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public ValueTask<BudgetLedgerReconciliationResult> ReconcileAsync(BudgetLedgerReconciliationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public ValueTask<BudgetOverrunHoldResolutionResult> ResolveOverrunHoldAsync(BudgetOverrunHoldResolutionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
