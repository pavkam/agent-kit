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
    /// <param name="executionLaneId">The lane that owns the run's session mutations and receives the call records.</param>
    /// <param name="catalogSnapshot">The run's captured catalog, whose execution-policy references become the capability's bindings.</param>
    /// <param name="runBudget">The run budget when the run declared limits; otherwise null.</param>
    /// <param name="runBudgetCapability">The run-scoped budget capability when the run is budgeted; otherwise null.</param>
    /// <param name="hooks">Optional hook binding forwarded to the tool executor; null when hooks are inactive.</param>
    /// <returns>The capability passed to <see cref="IToolExecutor.ExecuteAsync"/>.</returns>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    internal static ToolExecutionCapability Create(
        AgentLoopRunRequest request,
        AgentRunServices services,
        InRunOperationCorrelation turnCorrelation,
        ExecutionLaneId executionLaneId,
        ToolCatalogSnapshot catalogSnapshot,
        RunBudget? runBudget,
        BudgetExecutionCapability? runBudgetCapability,
        ToolExecutionHookBinding? hooks = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(catalogSnapshot);

        var runCoordinator = services.RunCoordinator ?? UnsupportedRunCoordinator.Instance;
        var session = new SessionExecutionCapability(request.SessionProfile, services.Session, runCoordinator);
        IBudgetScope scope;
        BudgetProfileKey profileKey;
        BudgetProfileVersion profileVersion;
        if (runBudget is not null && runBudgetCapability is not null)
        {
            scope = runBudget.GetScopeForToolExecution();
            profileKey = runBudgetCapability.ProfileKey;
            profileVersion = runBudgetCapability.ProfileVersion;
        }
        else
        {
            scope = new ToolBatchBudgetScope(
                new BudgetScopeId(Guid.Parse("00000000-0000-0000-0000-000000000001")),
                new BudgetScopeAddress(
                    request.Identity.TenantId,
                    request.Identity.PrincipalId,
                    request.AgentId,
                    request.SessionId,
                    request.RunId,
                    turnCorrelation.OperationId));
            profileKey = new BudgetProfileKey("tool-batch");
            profileVersion = new BudgetProfileVersion(1);
        }

        var budget = new BudgetExecutionCapability(
            profileKey,
            profileVersion,
            request.Identity,
            turnCorrelation,
            scope);

        var bindings = catalogSnapshot.ExecutionPolicies.Values
            .Distinct()
            .Select(static reference => new ToolExecutionPolicyBinding(reference))
            .ToImmutableArray();
        return new ToolExecutionCapability(
            session,
            budget,
            new ToolCallSessionTarget(request.BranchId, executionLaneId),
            bindings,
            hooks);
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
                    observedValue: BudgetQuantity.FromDecimal(0),
                    requestedAmount: BudgetQuantity.FromDecimal(1),
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
