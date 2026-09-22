// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One capability-scoped request to read a bounded file target.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record FileReadRequest
{
    /// <summary>Initializes a new instance of the <see cref="FileReadRequest"/> record.</summary>
    /// <param name="id">The operation identity for this read.</param>
    /// <param name="causalOperationId">The causal operation that requested the read.</param>
    /// <param name="agentId">The agent definition that owns the read.</param>
    /// <param name="runId">The active run when the read is in-run, if any.</param>
    /// <param name="target">The logical target to read.</param>
    /// <param name="bounds">The byte bounds the reader must enforce.</param>
    public FileReadRequest(
        FileOperationId id,
        OperationId causalOperationId,
        AgentId agentId,
        RunId? runId,
        FileTarget target,
        FileReadBounds bounds)
    {
        Id = id;
        CausalOperationId = causalOperationId;
        AgentId = agentId;
        RunId = runId;
        Target = target;
        Bounds = bounds;
    }

    /// <summary>Gets the operation identity for this read.</summary>
    public FileOperationId Id { get; init; }

    /// <summary>Gets the causal operation that requested the read.</summary>
    public OperationId CausalOperationId { get; init; }

    /// <summary>Gets the agent definition that owns the read.</summary>
    public AgentId AgentId { get; init; }

    /// <summary>Gets the active run when the read is in-run, if any.</summary>
    public RunId? RunId { get; init; }

    /// <summary>Gets the logical target to read.</summary>
    public FileTarget Target { get; init; }

    /// <summary>Gets the byte bounds the reader must enforce.</summary>
    public FileReadBounds Bounds { get; init; }
}
