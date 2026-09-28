// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Executes and recovers recoverable operations through the configured durability runtime.</summary>
public interface IDurableExecutionCoordinator
{
    /// <summary>Executes one recoverable operation under durability policy.</summary>
    /// <param name="operation">The immutable operation declaration.</param>
    /// <param name="hooks">Optional hook dispatch context for live invocation only.</param>
    /// <param name="cancellationToken">Cancels local awaiting; external state remains authoritative.</param>
    /// <returns>The terminal durable operation result.</returns>
    public Task<DurableOperationResult> ExecuteAsync(
        RecoverableOperationDescriptor operation,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken = default);

    /// <summary>Recovers one operation from durable evidence.</summary>
    /// <param name="address">The operation coordinates to recover.</param>
    /// <param name="context">The captured durability composition persisted for the operation.</param>
    /// <param name="hooks">Optional hook dispatch context; null when no active run exists.</param>
    /// <param name="cancellationToken">Cancels local recovery awaiting.</param>
    /// <returns>The terminal durable operation result.</returns>
    public Task<DurableOperationResult> RecoverAsync(
        DurableOperationAddress address,
        DurableExecutionContext context,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken = default);
}
