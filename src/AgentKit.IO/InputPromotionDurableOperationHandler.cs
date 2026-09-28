// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Owns the input-promotion boundary for the durability coordinator.</summary>
/// <remarks>
/// A distinct type per boundary keeps additive registration idempotent: repeating this package's registration adds
/// no second handler for the same operation name, which the coordinator rejects outright. The handler performs no
/// promotion itself; the component driving the boundary publishes its live continuation into the shared
/// <see cref="DurableBoundaryRegistry"/>, and a recovering process that holds none refuses rather than promoting
/// input nobody asked it to promote.
/// </remarks>
public sealed class InputPromotionDurableOperationHandler
    : DurableBoundaryHandler
{
    /// <summary>Initializes the input-promotion handler over the engine-wide continuation registry.</summary>
    /// <param name="registry">The non-null registry the running promotion publishes its live attempt into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is <see langword="null"/>.</exception>
    public InputPromotionDurableOperationHandler(DurableBoundaryRegistry registry)
        : base(IoDurableOperations.InputPromotion, registry)
    {
    }
}
