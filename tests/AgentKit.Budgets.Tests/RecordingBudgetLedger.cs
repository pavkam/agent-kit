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
        return ValueTask.FromResult(CreateResult);
    }

    public ValueTask<BudgetLedgerBatchReserveResult> ReserveBatchAsync(BudgetLedgerBatchReserveRequest request, CancellationToken cancellationToken = default)
    {
        ReserveRequest = request;
        return ValueTask.FromResult(ReserveResult);
    }

    public ValueTask<BudgetStartResult> MarkStartedAsync(BudgetLedgerReservationReference reservation, CancellationToken cancellationToken = default)
    {
        StartReference = reservation;
        return ValueTask.FromResult(StartResult);
    }

    public ValueTask<BudgetCommitResult> SettleAsync(BudgetLedgerSettlementRequest request, CancellationToken cancellationToken = default)
    {
        SettlementRequest = request;
        return ValueTask.FromResult(CommitResult);
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
        return ValueTask.FromResult(CorrectionResult);
    }

    public ValueTask<BudgetSnapshot> GetSnapshotAsync(BudgetLedgerScopeReference scope, CancellationToken cancellationToken = default) => ValueTask.FromResult(SnapshotResult);
    public ValueTask<BudgetUnresolvedReservationPage> ReadUnresolvedStartedAsync(BudgetUnresolvedReservationQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public ValueTask<BudgetLedgerReconciliationResult> ReconcileAsync(BudgetLedgerReconciliationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public ValueTask<BudgetOverrunHoldResolutionResult> ResolveOverrunHoldAsync(BudgetOverrunHoldResolutionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
