// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

/// <summary>Retains the exact provisional or acquired process-local owner of one execution lane.</summary>
internal sealed record SessionRunSlotOwner
{
    /// <summary>Initializes immutable exact owner evidence.</summary>
    /// <param name="leaseId">The non-default local lease identity.</param>
    /// <param name="operationId">The non-default accepted operation.</param>
    /// <param name="runId">The non-default accepted run.</param>
    /// <param name="stateRevision">The positive accepted total-state revision.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity or revision is default.</exception>
    internal SessionRunSlotOwner(SessionLeaseId leaseId, OperationId operationId, RunId runId,
        OperationStateRevision stateRevision)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(leaseId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(operationId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(stateRevision, default);
        LeaseId = leaseId;
        OperationId = operationId;
        RunId = runId;
        StateRevision = stateRevision;
    }

    /// <summary>Gets the exact local lease.</summary><value>A non-default identity.</value>
    internal SessionLeaseId LeaseId { get; }
    /// <summary>Gets the accepted operation.</summary><value>A non-default identity.</value>
    internal OperationId OperationId { get; }
    /// <summary>Gets the accepted run.</summary><value>A non-default identity.</value>
    internal RunId RunId { get; }
    /// <summary>Gets the accepted total-state revision.</summary><value>A positive revision.</value>
    internal OperationStateRevision StateRevision { get; }
}
