// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Performs one live boundary's effect under the ownership the durability coordinator acquired for it.</summary>
/// <param name="context">
/// The non-null coordinator-assembled invocation context. It carries the accepted declaration, the active lease, and
/// the coordinator-owned writer the boundary uses for mid-operation checkpoints and waiting records.
/// </param>
/// <param name="cancellationToken">
/// Cancels local awaiting only; it never asserts that an already-started effect stopped.
/// </param>
/// <returns>The non-null terminal durable result for the boundary's effect attempt.</returns>
/// <remarks>
/// A continuation of this shape is published into a <see cref="DurableBoundaryRegistry"/> for exactly one operation
/// identity. It closes over live run state that no payload can reconstruct, which is precisely why a recovering
/// process finds none and must refuse rather than invent a terminal record.
/// </remarks>
public delegate ValueTask<DurableOperationResult> DurableBoundaryInvocation(
    DurableInvocationContext context,
    CancellationToken cancellationToken);
