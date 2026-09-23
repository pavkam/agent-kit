// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

/// <summary>Composes invocation-only <see cref="ToolExecutionCapability"/> values for tool batches.</summary>
internal static class ToolExecutionCapabilityFactory
{
    /// <summary>Creates the session and budget bindings for one tool batch.</summary>
    /// <param name="request">The active run request.</param>
    /// <param name="services">The compiled run services.</param>
    /// <param name="turnCorrelation">The turn operation correlation.</param>
    /// <param name="runBudget">The run budget when the run declared limits; otherwise null.</param>
    /// <param name="hooks">Optional hook binding forwarded to the tool executor; null when hooks are inactive.</param>
    /// <returns>The capability passed to <see cref="IToolExecutor.ExecuteAsync"/>.</returns>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    internal static ToolExecutionCapability Create(
        AgentLoopRunRequest request,
        AgentRunServices services,
        InRunOperationCorrelation turnCorrelation,
        RunBudget? runBudget,
        ToolExecutionHookBinding? hooks = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(services);

        var runCoordinator = services.RunCoordinator ?? UnsupportedRunCoordinator.Instance;
        var session = new SessionExecutionCapability(request.SessionProfile, services.Session, runCoordinator);
        // The legacy adapter does not reserve through this capability; the loop still counts through
        // <see cref="RunBudget"/> when present. Bind a shape-valid scope address for the active turn only.
        _ = runBudget;
        var budget = new BudgetExecutionCapability(
            new BudgetProfileKey("tool-batch"),
            new BudgetProfileVersion(1),
            request.Identity,
            turnCorrelation,
            new ToolBatchBudgetScope(
                new BudgetScopeId(Guid.Parse("00000000-0000-0000-0000-000000000001")),
                new BudgetScopeAddress(
                    request.Identity.TenantId,
                    request.Identity.PrincipalId,
                    request.AgentId,
                    request.SessionId,
                    request.RunId,
                    turnCorrelation.OperationId)));

        return new ToolExecutionCapability(session, budget, hooks);
    }

    /// <summary>A budget scope that rejects every reservation; used only to satisfy capability shape for unbudgeted runs.</summary>
    private sealed class ToolBatchBudgetScope(BudgetScopeId id, BudgetScopeAddress address): IBudgetScope
    {
        public BudgetScopeId Id { get; } = id;
        public BudgetScopeAddress Address { get; } = address;

        public ValueTask<BudgetReservationResult> ReserveAsync(BudgetReservationRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return ValueTask.FromResult<BudgetReservationResult>(
                new BudgetRejected(new BudgetLimitFailure(
                    Id,
                    request.Dimension,
                    BudgetLimitKind.Hard,
                    configuredValue: 0,
                    observedValue: new BudgetQuantity(System.Numerics.BigInteger.Zero, 0),
                    requestedAmount: new BudgetQuantity(System.Numerics.BigInteger.One, 0),
                    request.Unit,
                    "Unbudgeted runs do not reserve tool budget dimensions through this scope.")));
        }

        public ValueTask<BudgetBatchReservationResult> ReserveBatchAsync(
            ImmutableArray<BudgetReservationRequest> requests,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<BudgetSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class UnsupportedRunCoordinator: ISessionRunCoordinator
    {
        internal static UnsupportedRunCoordinator Instance { get; } = new();

        public ValueTask<SessionRunLeaseResult> AcquireAsync(
            SessionRunLeaseRequest request,
            SessionExecutionCapability session,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
