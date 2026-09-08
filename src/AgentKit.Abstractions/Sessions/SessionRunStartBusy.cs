// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that another durable operation already owns the requested lane.</summary>
public sealed record SessionRunStartBusy: SessionRunStartResult
{
    /// <summary>Initializes a lane-busy result.</summary><param name="operationId">The authorized current operation identity.</param><param name="runId">The authorized current run identity.</param>
    public SessionRunStartBusy(OperationId operationId, RunId runId) { ArgumentOutOfRangeException.ThrowIfEqual(operationId, default); ArgumentOutOfRangeException.ThrowIfEqual(runId, default); OperationId = operationId; RunId = runId; }
    /// <summary>Gets current operation identity.</summary><value>The non-default installed operation.</value>
    public OperationId OperationId { get; }
    /// <summary>Gets current run identity.</summary><value>The non-default installed run.</value>
    public RunId RunId { get; }
}
