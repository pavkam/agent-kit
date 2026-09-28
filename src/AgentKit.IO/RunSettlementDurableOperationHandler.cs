// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Owns the run-settlement boundary for the durability coordinator.</summary>
/// <remarks>
/// A distinct type per boundary keeps additive registration idempotent: repeating this package's registration adds
/// no second handler for the same operation name, which the coordinator rejects outright. Settlement evidence is
/// written by the component that actually settled the run, so this handler only bridges the coordinator to that live
/// continuation and refuses when none is published.
/// </remarks>
public sealed class RunSettlementDurableOperationHandler
    : DurableBoundaryHandler
{
    /// <summary>Initializes the run-settlement handler over the engine-wide continuation registry.</summary>
    /// <param name="registry">The non-null registry the settling run publishes its live attempt into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is <see langword="null"/>.</exception>
    public RunSettlementDurableOperationHandler(DurableBoundaryRegistry registry)
        : base(IoDurableOperations.RunSettlement, registry)
    {
    }
}
