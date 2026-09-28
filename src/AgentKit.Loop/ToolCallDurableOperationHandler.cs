// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

/// <summary>Owns the loop's tool-call boundary for the durability coordinator.</summary>
/// <remarks>
/// A distinct type per boundary keeps additive registration idempotent: repeating the loop's registration adds no
/// second handler for the same operation name, which the coordinator rejects outright.
/// </remarks>
public sealed class ToolCallDurableOperationHandler
    : DurableBoundaryHandler
{
    /// <summary>Initializes the tool-call handler over the engine-wide continuation registry.</summary>
    /// <param name="registry">The non-null registry the running loop publishes its live invocation into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    public ToolCallDurableOperationHandler(DurableBoundaryRegistry registry)
        : base(LoopDurableOperations.ToolCall, registry)
    {
    }
}
