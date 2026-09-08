// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The exact lane already has an active process-local owner and the configured behavior did not acquire it.</summary>
public sealed record SessionRunBusy: SessionRunLeaseResult
{
    /// <summary>Initializes a busy result carrying the actual current owner.</summary>
    /// <param name="activeOperationId">The accepted operation currently holding the lane.</param>
    /// <param name="activeRunId">The run currently holding the lane.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default.</exception>
    public SessionRunBusy(OperationId activeOperationId, RunId activeRunId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(activeOperationId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(activeRunId, default);
        ActiveOperationId = activeOperationId;
        ActiveRunId = activeRunId;
    }

    /// <summary>Gets the current operation owner.</summary><value>The exact non-default accepted operation.</value>
    public OperationId ActiveOperationId { get; }
    /// <summary>Gets the current run owner.</summary><value>The exact non-default run.</value>
    public RunId ActiveRunId { get; }
}
