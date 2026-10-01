// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

/// <summary>Composes invocation-only <see cref="ToolExecutionCapability"/> values for tool batches.</summary>
internal static class ToolExecutionCapabilityFactory
{
    /// <summary>Creates the session and budget bindings for one tool batch; a run that declared no limits carries no budget binding.</summary>
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
        // A run that declared no limits has no scope to reserve through, so the executor reserves no tool budget dimension.
        var budget = runBudget is not null && runBudgetCapability is not null
            ? new BudgetExecutionCapability(
                runBudgetCapability.ProfileKey,
                runBudgetCapability.ProfileVersion,
                request.Identity,
                turnCorrelation,
                runBudget.GetScopeForToolExecution())
            : null;

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
