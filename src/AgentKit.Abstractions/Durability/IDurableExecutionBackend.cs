// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Performs one external durable handoff or reconciliation attempt.</summary>
public interface IDurableExecutionBackend
{
    /// <summary>Gets the immutable descriptor for this backend registration.</summary>
    public DurableBackendDescriptor Descriptor { get; }

    /// <summary>Dispatches work to the external durable owner when required.</summary>
    /// <param name="request">The immutable dispatch request.</param>
    /// <param name="cancellationToken">Cancels before the handoff attempt completes.</param>
    /// <returns>A closed dispatch outcome.</returns>
    public ValueTask<DurableDispatchResult> DispatchAsync(
        DurableDispatchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Reconciles unknown external effect state when supported.</summary>
    /// <param name="request">The immutable reconciliation request.</param>
    /// <param name="cancellationToken">Cancels before reconciliation completes.</param>
    /// <returns>A closed reconciliation outcome.</returns>
    public ValueTask<DurableReconciliationResult> ReconcileAsync(
        DurableReconciliationRequest request,
        CancellationToken cancellationToken = default);
}
