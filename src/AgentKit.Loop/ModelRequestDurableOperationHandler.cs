// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

/// <summary>Owns the loop's model-attempt boundary for the durability coordinator.</summary>
/// <remarks>
/// A distinct type per boundary keeps additive registration idempotent: repeating the loop's registration adds no
/// second handler for the same operation name, which the coordinator rejects outright.
/// </remarks>
public sealed class ModelRequestDurableOperationHandler
    : DurableBoundaryHandler
{
    /// <summary>Initializes the model-attempt handler over the engine-wide continuation registry.</summary>
    /// <param name="registry">The non-null registry the running loop publishes its live attempt into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    public ModelRequestDurableOperationHandler(DurableBoundaryRegistry registry)
        : base(LoopDurableOperations.ModelRequest, registry)
    {
    }
}
